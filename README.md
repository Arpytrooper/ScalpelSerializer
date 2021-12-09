# Replacer

Yes, possibly the most uninspiring name imaginable. Suggestions welcome.

The goal of the tool is automatically fix script references in unity asset files, with the goal of enabling in editor testing for H3VR.

The tool will scan a tree of decompiled assemblies, and a tree of assets and patch any mScript references so that they refer to the script files in from the decompiled assemblies.

In adition to the dynamic script reference replacements you can:

 - Specify General GUID replacements
 - Specify individual script replacements

# Usage
Configure the tool using appsettings.json

**Assembly Bindings**
Specify a directory and a GUID.
Each folder provided will be scanned recursively for scripts.
The given GUID binding is used as the assembly GUID.

**PrefabsPath**
Specify a directory that contains the assets that need to be patched.

**AssetTypes**
Specify a list of file filters to match the types of files you wish to patch.
Note that all files are assumed to be text files.

**GuidReplacements**
Specify 0 or more pairs of GUIDS, **[old, new]**.
For all files processed in the PrefabsPath, any instance of an **old** GUID will be replaced with **new** GUID.

**ScriptReplacements**
Specify 0 or more sets of script replacements.
**[ "oldguid", "newguid", "newfileId" ]**
For all files processed in the PrefabsPath any script reference matching **oldguid** will be replaced with **newguid**, **newfileId**
