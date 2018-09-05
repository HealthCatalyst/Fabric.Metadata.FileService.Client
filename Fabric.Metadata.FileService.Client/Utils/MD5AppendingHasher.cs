namespace Fabric.Metadata.FileService.Client.Utils
{
    using System;
    using System.IO;
    using System.Security.Cryptography;

    public class Md5AppendingHasher : IDisposable
    {
        // ReSharper disable once InconsistentNaming
        private readonly MD5 md5Hasher = MD5.Create();

        public Md5AppendingHasher()
        {
            md5Hasher.Initialize();
        }

        public int Append(Stream stream)
        {
            byte[] data;
            using (var memoryStream = new MemoryStream())
            {
                stream.Seek(0, SeekOrigin.Begin);
                stream.CopyTo(memoryStream);
                data = memoryStream.ToArray();
            }

            return md5Hasher.TransformBlock(data, 0, data.Length, data, 0);
        }

        public string FinalizeAndGetHash()
        {
            AppendFinal();
            // from https://stackoverflow.com/questions/10520048/calculate-md5-checksum-for-a-file
            var md5Hash = this.md5Hasher.Hash;
            return BitConverter.ToString(md5Hash).Replace("-", string.Empty).ToLowerInvariant();
        }

        private void AppendFinal()
        {
            md5Hasher.TransformFinalBlock(new byte[0], 0, 0);
        }

        public void Dispose()
        {
            md5Hasher?.Dispose();
        }

    }
}
