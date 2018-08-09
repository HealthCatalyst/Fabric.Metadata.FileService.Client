using System;

namespace Fabric.Metadata.FileService.Client
{
    public class UploadSession
    {
        /// <summary>
        /// Gets or sets SessionId
        /// </summary>
        public Guid SessionId { get; set; }

        /// <summary>
        /// Gets or sets FileUploadChunkSizeInBytes
        /// </summary>
        public long FileUploadChunkSizeInBytes { get; set; }

        /// <summary>
        /// Gets or sets FileUploadMaxFileSizeInMegabytes
        /// </summary>
        public long FileUploadMaxFileSizeInMegabytes { get; set; }
    }
}