using System.Diagnostics.Contracts;

namespace Fabric.Metadata.FileService.Client
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Threading.Tasks;

    /// <inheritdoc />
    public class FileSplitter : IFileSplitter
    {
        const int ReadbufferSize = 1024 * 1024; // 1MB

        public async Task<IList<FilePart>> SplitFile(string filePath, string fileName, string tempFolder,
            long chunkSizeInBytes, long maxFileSizeInMegabytes, Func<Stream, FilePart, Task> fnActionForStream)
        {
            if (string.IsNullOrWhiteSpace(filePath)) throw new ArgumentNullException(nameof(filePath));
            if (string.IsNullOrWhiteSpace(fileName)) throw new ArgumentNullException(nameof(fileName));
            if (string.IsNullOrWhiteSpace(tempFolder)) throw new ArgumentNullException(nameof(tempFolder));
            if (chunkSizeInBytes <= 0) throw new ArgumentOutOfRangeException(nameof(chunkSizeInBytes));
            if (maxFileSizeInMegabytes <= 0) throw new ArgumentOutOfRangeException(nameof(maxFileSizeInMegabytes));

            // first check tht file is not too big
            var fileSize = new FileInfo(filePath).Length;
            var maxFileSizeInBytes = (maxFileSizeInMegabytes * 1024 * 1024);
            if (fileSize > maxFileSizeInBytes)
            {
                throw new Exception($"File {filePath} is too big {fileSize} bytes while server allows {maxFileSizeInBytes}");
            }

            var md5FileHasher = new MD5FileHasher();

            bool rslt = false;
            string baseFileName = Path.GetFileName(filePath);
            // set the size of file chunk we are going to split into  
            long bufferChunkSize = chunkSizeInBytes;
            // set a buffer size and an array to store the buffer data as we read it  
            byte[] fsBuffer = new byte[ReadbufferSize];
            // var fileInfo = new FileInfo(FileName);

            var fileParts = new List<FilePart>();
            // open the file to read it into chunks  
            using (FileStream fullFileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                // calculate the number of files that will be created  
                int totalFileParts = 0;
                totalFileParts = GetCountOfFileParts(bufferChunkSize, fullFileStream.Length);

                int filePartCount = 0;
                byte[] data = new byte[ReadbufferSize];
                // scan through the file, and each time we get enough data to fill a chunk, write out that file  
                while (fullFileStream.Position < fullFileStream.Length)
                {
                    string filePartNameOnly =
                        $"{baseFileName}.part_{(filePartCount + 1).ToString()}.{totalFileParts.ToString()}";

                    var fullPathTofilePart = Path.Combine(tempFolder, filePartNameOnly);

                    using (var memoryStream = new MemoryStream(data, true))
                    {
                        memoryStream.Seek(0, SeekOrigin.Begin);

                        var bytesRemaining = Convert.ToInt32(bufferChunkSize);
                        int bytesRead = 0;
                        var bytesToRead = Math.Min(bytesRemaining, ReadbufferSize);

                        while (bytesRemaining > 0 && (bytesRead = await fullFileStream.ReadAsync(fsBuffer, 0,
                         bytesToRead)) > 0)
                        {
                            await memoryStream.WriteAsync(fsBuffer, 0, bytesRead);
                            bytesRemaining -= bytesRead;
                        }

                        if (bytesToRead < ReadbufferSize)
                        {
                            memoryStream.SetLength(bytesToRead);
                        }

                        var filePart = new FilePart
                        {
                            Id = filePartCount,
                            Offset = Convert.ToInt32(fullFileStream.Position),
                            Size = Convert.ToInt32(bytesToRead),
                            FullPath = fullPathTofilePart,
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
                float preciseFileParts = ((float) fileLength / (float) bufferChunkSize);
                totalFileParts = (int) Math.Ceiling(preciseFileParts);
            }

            return totalFileParts;
        }
    }
}
