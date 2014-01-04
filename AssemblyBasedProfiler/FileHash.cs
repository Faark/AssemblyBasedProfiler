using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

namespace AssemblyBasedProfiller
{
    class FileHash
    {
        static System.Security.Cryptography.MD5 md5 = System.Security.Cryptography.MD5.Create();
        long dataSize;
        byte[] hashData;
        public FileHash(FileInfo file)
        {
            using (var stream = file.OpenRead())
            {
                dataSize = stream.Length;
                hashData = md5.ComputeHash(stream);
            }
        }
        public FileHash(Stream fileData)
        {
            dataSize = fileData.Length;
            hashData = md5.ComputeHash(fileData);
        }
        public bool EqualsTo(FileHash otherHash)
        {
            return dataSize == otherHash.dataSize && hashData.SequenceEqual(otherHash.hashData);
        }
        public bool EqualsTo(FileInfo file)
        {
            return dataSize == file.Length && EqualsTo(new FileHash(file));
        }
    }
}
