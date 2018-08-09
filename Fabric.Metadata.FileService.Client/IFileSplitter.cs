﻿namespace Fabric.Metadata.FileService.Client
{
    using System.Collections.Generic;
    using System.Threading.Tasks;

    public interface IFileSplitter
    {
        Task<IList<FilePart>> SplitFile(string filePath, string fileName, string tempFolder,
            long chunkSizeInBytes, long maxFileSizeInMegabytes);
    }
}