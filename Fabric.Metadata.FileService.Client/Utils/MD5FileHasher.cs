namespace Fabric.Metadata.FileService.Client.Utils
{
    using System;
    using System.Diagnostics.Contracts;
    using System.IO;
    using System.Security.Cryptography;

    // ReSharper disable once InconsistentNaming
    public class MD5FileHasher
    {
        private readonly MD5 md5Hasher = MD5.Create();

        // ReSharper disable once InconsistentNaming
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Globalization", "CA1308:NormalizeStringsToUppercase", Justification = "MD5 hash should be lower case")]
        [Pure]
        public string CalculateHashForFile(string filePath)
        {
            if (String.IsNullOrWhiteSpace(filePath)) throw new ArgumentNullException(nameof(filePath));

            using (FileStream stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                return CalculateHashForStream(stream);
            }

        }

        // ReSharper disable once InconsistentNaming
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Globalization", "CA1308:NormalizeStringsToUppercase", Justification = "MD5 hash should be lower case")]
        [Pure]
        public string CalculateHashForStream(Stream stream)
        {
            // from https://stackoverflow.com/questions/10520048/calculate-md5-checksum-for-a-file
            var md5Hash = this.md5Hasher.ComputeHash(stream);
            return BitConverter.ToString(md5Hash).Replace("-", string.Empty).ToLowerInvariant();
        }
    }
}
