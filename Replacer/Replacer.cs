using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Configuration;

namespace Replacer
{
    class Replacer
    {
        private readonly IConfigurationRoot _configuration;
        private readonly Dictionary<string, Assembly> _assemblyDict;

        private struct Assembly
        {
            public string NewGuid { get; set; }
            public string File { get; set; }
        }


        /// <summary>
        /// Replacer scans one or more folders of decompiled assemblies, and a folder of Unity assets and attempts to
        /// edit the assets to fix broken assembly references.
        /// </summary>
        /// <param name="configuration"></param>
        public Replacer(IConfigurationRoot configuration)
        {
            //Grab config from appsettings.json
            _configuration = configuration;
            var assetFilters = _configuration.GetSection("AssetTypes").Get<string[]>();
            var assemblyBindings = _configuration.GetSection("AssemblyBindings").Get<string[][]>();
            var prefabDir = _configuration["PrefabsPath"];
            _assemblyDict = BuildAssemblyDict(assemblyBindings);
            ProcessAssets(prefabDir, assetFilters.ToList());
        }

        /// <summary>
        /// Scans a directory (and its subdirs) for assets matching assetFilters.
        /// For each asset attempts to cross check referenced scripts and patch broken refs.
        /// </summary>
        /// <param name="assetDir">Root directory of assets to patch</param>
        /// <param name="assetFilters">List of filters that will be used to select appropriate fiel types. E.g. '*.asset' </param>
        private void ProcessAssets(string assetDir,  List<string> assetFilters)
        {
            Console.WriteLine($"Scanning Assets...");
            var files = EnumerateDirectory(assetDir, assetFilters);
            Console.WriteLine($"Found {files.Count} assets.");
            var progressCount = 1;
            foreach (var file in files)
            {
                Console.WriteLine($"Processing file: {file}. {progressCount}/{files.Count}");
                ProcessFile(file);
                Console.WriteLine($"Finished processing: {file}");
                progressCount++;
            }
        }

        /// <summary>
        /// For a given filePath, inspect the file and attempt to patch any script references.
        /// A buffer file is used for output during the operation and the original file is overwritten atomically.
        /// </summary>
        /// <param name="filePath">Path to an asset file</param>
        private void ProcessFile(string filePath)
        {
            using (var originalFile = File.OpenText(filePath))
            using (var editedFile = new StreamWriter("buffer.txt")) //use a buffer so we can write to the file while streaming through it.
            {
                var regex = new Regex(@"^\s*?m_Script: {fileID: (\d+), guid: (.*?),.*$");
                string line;
                while ((line = originalFile.ReadLine()) != null) //read through the whole file, line by line
                {
                    var matches = regex.Matches(line);
                    if (matches.Count > 0) //if there is a script reference
                    {
                        var fileId = matches[0].Groups[1].Value; //extract the current fileId
                        var guid = matches[0].Groups[2].Value; //extract the current guid
                        if (_assemblyDict.ContainsKey(guid))
                        {
                            //the assemblyDict contains a reference to the meta file; we need to truncate it to get the script's file name
                            var fileName = _assemblyDict[guid].File.Replace(".meta", ""); 
                            
                            var newFileId = CalcFileId(fileName); //calc the new fileId then update the references
                            line = line.Replace(fileId, newFileId);
                            line = line.Replace(guid, _assemblyDict[guid].NewGuid);
                        }
                        
                    }
                    editedFile.WriteLine(line);
                }
            }

            try
            {// file juggling to prevent issues with writing to a file were reading from.
                var backupPath = $"{filePath}.old";
                File.Copy(filePath, backupPath);
                if (File.Exists(backupPath))
                {
                    File.Delete(filePath);
                    File.Move("buffer.txt", filePath);
                    File.Delete(backupPath); // remove this if you want to save a backup of the unaltered file.
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
        }

        /// <summary>
        /// For each assembly dir, finds all the .meta files and catalogs them indexed by the file's guid.
        /// </summary>
        /// <param name="assemblyBindings">Dict containing all .meta files, keyed by guid.</param>
        /// <returns></returns>
        private static Dictionary<string, Assembly> BuildAssemblyDict(string[][] assemblyBindings)
        {
            var assemblyDict = new Dictionary<string, Assembly>();
            var files = new List<string>();
            //Assemblies will have fixed guids, so we read the bindings from config.
            //For each binding we scan the directory for .meta files and tag them with the appropriate new guid, indexing by old guid.
            foreach (var assemblyBinding in assemblyBindings)
            {
                var assemblyDir = assemblyBinding[0];
                var assemblyGuid = assemblyBinding[1];

                Console.WriteLine($"Scanning Assemblies...");
                files.AddRange(EnumerateDirectory(assemblyDir, new List<string>() { "*.meta" }));
                Console.WriteLine($"Found {files.Count} .meta files in assembly: {assemblyDir}");

                foreach (var file in files)
                {
                    using (var scriptFile = File.OpenText(file))
                    {
                        string line;
                        while ((line = scriptFile.ReadLine()) != null)
                        {
                            var regex = new Regex("^guid: (.*?)$");
                            var matches = regex.Matches(line);
                            if (matches.Count > 0)
                            {
                                var guid = matches[0].Groups[1].Value;
                                if (!assemblyDict.ContainsKey(guid))
                                {
                                    assemblyDict.Add(matches[0].Groups[1].Value, new Assembly() { File = file, NewGuid = assemblyGuid });
                                }
                            }
                        }
                    }
                }
            }

            return assemblyDict;
        }

        /// <summary>
        /// Parses a given .cs file for its namespace and class name.
        /// These are used to calculate a hash for that file.
        /// The first 4 bytes of the hash represent the files' fileId.
        /// </summary>
        /// <param name="file"></param>
        /// <returns></returns>
        private string CalcFileId(string file)
        {
            string foundNamespace = null;
            string foundClassname = null;

            using (var scriptFile = File.OpenText(file))
            {
                string line;
                while ((line = scriptFile.ReadLine()) != null)
                {
                    var rxNamespace = new Regex(@"^namespace (.*)$");
                    var rxClassName = new Regex(@"^.*class (.*?)\s.*?$");
                    var namespaceMatches = rxNamespace.Matches(line);
                    var nameMatches = rxClassName.Matches(line);
                    if (namespaceMatches.Count > 0)
                    {
                        foundNamespace = namespaceMatches[0].Groups[1].Value;
                    }
                    if (nameMatches.Count > 0)
                    {
                        foundClassname = nameMatches[0].Groups[1].Value;
                    }

                    if (!string.IsNullOrEmpty(foundNamespace) && !string.IsNullOrEmpty(foundClassname))
                    {
                        return FileIDUtil.Compute(foundNamespace, foundClassname).ToString();
                    }
                }

                if (!string.IsNullOrEmpty(foundClassname) && string.IsNullOrEmpty(foundNamespace))
                {
                    //If there is no defined namespace then use default
                    foundNamespace = "";
                    return FileIDUtil.Compute(foundNamespace, foundClassname).ToString();
                }
            }

            return null;
        }

        /// <summary>
        /// Returns a list of strings that represent the full path for each file matching the filters found within.
        /// Searches recursively.
        /// </summary>
        /// <param name="path"></param>
        /// <param name="filters"></param>
        /// <returns></returns>
        private static List<string> EnumerateDirectory(string path, List<string> filters = null)
        {
            if (filters == null)
            {
                filters = new List<string>() {"*"};
            }
            Console.WriteLine($"Scanning Root Dir: {path}");
            if (!Directory.Exists(path))
            {
                throw new DirectoryNotFoundException($"Input directory '{path}' does not exist.");
            }
            else
            {
                var files = new List<string>();
                foreach (var filter in filters)
                {
                    files.AddRange( Directory.EnumerateFiles(path, filter, SearchOption.AllDirectories).ToList());
                }
                return files;
            }
        }
    }
}
