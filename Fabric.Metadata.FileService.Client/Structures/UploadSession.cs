namespace Fabric.Metadata.FileService.Client.Structures
{
    using System;

    /// <summary>
    /// UploadSession
    /// </summary>
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

        /// <summary>
        /// Gets or sets SessionStartedBy
        /// </summary>
        public string SessionStartedBy { get; set; }

        /// <summary>
        /// Gets or sets SessionStartedDateTimeUtc
        /// </summary>
        public DateTime? SessionStartedDateTimeUtc { get; set; }

        /// <summary>
        /// Gets or sets SessionFinishedDateTimeUtc
        /// </summary>
        public DateTime? SessionFinishedDateTimeUtc { get; set; }

        /// <summary>
        /// Gets or sets FileHash
        /// </summary>
        public string FileHash { get; set; }

        /// <summary>
        /// Gets or sets FileUploadSessionExpirationInMinutes
        /// </summary>
        public long FileUploadSessionExpirationInMinutes { get; set; }

        /// <summary>
        /// Gets or sets FileName
        /// </summary>
        public string FileName { get; set; }
    }

}