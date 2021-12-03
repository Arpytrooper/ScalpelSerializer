using System;
using System.Security.Cryptography;

namespace Replacer
{
    public static class FileIDUtil
    {
        public static int Compute(Type t)
        {
            string toBeHashed = "s\0\0\0" + t.Namespace + t.Name;

            using (HashAlgorithm hash = new Md4())
            {
                byte[] hashed = hash.ComputeHash(System.Text.Encoding.UTF8.GetBytes(toBeHashed));

                int result = 0;

                for (int i = 3; i >= 0; --i)
                {
                    result <<= 8;
                    result |= hashed[i];
                }

                return result;
            }
        }

        public static int Compute(string namespace_str, string className)
        {
            string toBeHashed = "s\0\0\0" + namespace_str + className;

            using (HashAlgorithm hash = new Md4())
            {
                byte[] hashed = hash.ComputeHash(System.Text.Encoding.UTF8.GetBytes(toBeHashed));

                int result = 0;

                for (int i = 3; i >= 0; --i)
                {
                    result <<= 8;
                    result |= hashed[i];
                }

                return result;
            }
        }
    }
}