using System;
using System.IO;

namespace Fabric.Metadata.FileService.Client
{
    using System.Collections.Generic;
    using System.Threading.Tasks;

    public interface IFileSplitter
    {
        Task<IList<FilePart>> SplitFile(string filePath, string fileName,
            long chunkSizeInBytes, long maxFileSizeInMegabytes, Func<Stream, FilePart, Task> fnActionForStream);
    }
}