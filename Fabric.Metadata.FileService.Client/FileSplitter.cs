namespace Fabric.Metadata.FileService.Client
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Threading.Tasks;

    /// <inheritdoc />
    public class FileSplitter : IFileSplitter
    {
        const int ReadbufferSize = 1024;

        public async Task<IList<FilePart>> SplitFile(string filePath, string fileName, string tempFolder,
            long chunkSizeInBytes, long maxFileSizeInMegabytes)
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
                if (fullFileStream.Length < bufferChunkSize)
                {
                    totalFileParts = 1;
                }
                else
                {
                    float preciseFileParts = ((float)fullFileStream.Length / (float)bufferChunkSize);
                    totalFileParts = (int)Math.Ceiling(preciseFileParts);
                }

                int filePartCount = 0;
                // scan through the file, and each time we get enough data to fill a chunk, write out that file  
                while (fullFileStream.Position < fullFileStream.Length)
                {
                    string filePartNameOnly =
                        $"{baseFileName}.part_{(filePartCount + 1).ToString()}.{totalFileParts.ToString()}";

                    var fullPathTofilePart = Path.Combine(tempFolder, filePartNameOnly);
                    using (FileStream filePartStream = new FileStream(fullPathTofilePart, FileMode.Create))
                    {
                        var bytesRemaining = Convert.ToInt32(bufferChunkSize);
                        int bytesRead = 0;
                        while (bytesRemaining > 0 && (bytesRead = await fullFileStream.ReadAsync(fsBuffer, 0,
                         Math.Min(bytesRemaining, ReadbufferSize))) > 0)
                        {
                            await filePartStream.WriteAsync(fsBuffer, 0, bytesRead);
                            bytesRemaining -= bytesRead;
                        }
                    }

                    var length = new FileInfo(fullPathTofilePart).Length;

                    fileParts.Add(new FilePart
                    {
                        Id = filePartCount,
                        Offset = Convert.ToInt32(fullFileStream.Position),
                        Size = Convert.ToInt32(length),
                        FullPath = fullPathTofilePart,
                        Hash = md5FileHasher.CalculateHashForFile(fullPathTofilePart),
                    });

                    // file written, loop for next chunk  
                    filePartCount++;
                }
            }
            return fileParts;
        }
    }
}
