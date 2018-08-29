namespace Fabric.Metadata.FileService.Client.Interfaces
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Threading.Tasks;
    using Structures;

    public interface IFileSplitter
    {
        Task<IList<FilePart>> SplitFile(string filePath, string fileName,
            long chunkSizeInBytes, long maxFileSizeInMegabytes, Func<Stream, FilePart, Task> fnActionForStream);
    }
}