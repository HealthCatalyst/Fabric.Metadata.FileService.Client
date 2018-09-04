using System.Diagnostics.Contracts;

namespace Fabric.Metadata.FileService.Client
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Threading.Tasks;
    using Interfaces;
    using Structures;

    /// <inheritdoc />
    public class FileSplitter : IFileSplitter
    {
        public async Task<IList<FilePart>> SplitFile(string filePath, string fileName,
            long chunkSizeInBytes, long maxFileSizeInMegabytes, Func<Stream, FilePart, Task> fnActionForStream)
        {
            if (string.IsNullOrWhiteSpace(filePath)) throw new ArgumentNullException(nameof(filePath));
            if (string.IsNullOrWhiteSpace(fileName)) throw new ArgumentNullException(nameof(fileName));
            if (chunkSizeInBytes <= 0) throw new ArgumentOutOfRangeException(nameof(chunkSizeInBytes));
            if (maxFileSizeInMegabytes <= 0) throw new ArgumentOutOfRangeException(nameof(maxFileSizeInMegabytes));

            // first check tht file is not too big
            var fileSize = new FileInfo(filePath).Length;
            var maxFileSizeInBytes = (maxFileSizeInMegabytes * 1024 * 1024);
            if (fileSize > maxFileSizeInBytes)
            {
                throw new InvalidOperationException($"File {filePath} is too big {fileSize} bytes while server allows {maxFileSizeInBytes}");
            }

            var md5FileHasher = new MD5FileHasher();

            // set the size of file chunk we are going to split into  
            long bufferChunkSize = chunkSizeInBytes;

            var fileParts = new List<FilePart>();
            // open the file to read it into chunks  
            using (FileStream fullFileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                int filePartCount = 0;
                byte[] data = new byte[bufferChunkSize];

                // scan through the file, and each time we get enough data to fill a chunk, write out that file  
                while (fullFileStream.Position < fullFileStream.Length)
                {
                    long startOffset = fullFileStream.Position;

                    var bytesRead = await fullFileStream.ReadAsync(data, 0, Convert.ToInt32(bufferChunkSize));

                    using (var memoryStream = new MemoryStream(data, 0, Convert.ToInt32(bytesRead)))
                    {
                        memoryStream.Seek(0, SeekOrigin.Begin);
                        var filePart = new FilePart
                        {
                            Id = filePartCount,
                            Offset = startOffset,
                            Size = memoryStream.Length,
                            Hash = md5FileHasher.CalculateHashForStream(memoryStream),
                        };

                        await fnActionForStream(memoryStream, filePart);
                        fileParts.Add(filePart);
                    }

                    // file written, loop for next chunk  
                    filePartCount++;
                }
            }
            return fileParts;
        }

        [Pure]
        public int GetCountOfFileParts(long bufferChunkSize, long fileLength)
        {
            int totalFileParts;
            if (fileLength < bufferChunkSize)
            {
                totalFileParts = 1;
            }
            else
            {
                float preciseFileParts = (fileLength / (float) bufferChunkSize);
                totalFileParts = (int) Math.Ceiling(preciseFileParts);
            }

            return totalFileParts;
        }
    }
}
