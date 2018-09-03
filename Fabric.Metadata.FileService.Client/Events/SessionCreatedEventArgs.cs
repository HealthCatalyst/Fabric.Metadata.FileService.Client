namespace Fabric.Metadata.FileService.Client.Events
{
    using System;
    using System.ComponentModel;

    public class SessionCreatedEventArgs : CancelEventArgs
    {
        public SessionCreatedEventArgs(int resourceId, Guid sessionId, long chunkSizeInBytes, long maxFileSizeInMegabytes, 
            string sessionStartedBy, DateTime? sessionStartedDateTimeUtc, 
            long sessionExpirationInMinutes)
        {
            this.SessionId = sessionId;
            this.ChunkSizeInBytes = chunkSizeInBytes;
            this.MaxFileSizeInMegabytes = maxFileSizeInMegabytes;
            this.SessionStartedBy = sessionStartedBy;
            this.SessionStartedDateTimeUtc = sessionStartedDateTimeUtc;
            this.SessionExpirationInMinutes = sessionExpirationInMinutes;
            this.ResourceId = resourceId;
        }

        public int ResourceId { get; }

        public Guid SessionId { get; set; }

        /// <summary>
        /// Maximum file size allowed by the server
        /// </summary>
        public long MaxFileSizeInMegabytes { get; set; }
        
        /// <summary>
        /// What user started the upload session
        /// </summary>
        public string SessionStartedBy { get; }

        /// <summary>
        /// When was the upload session started
        /// </summary>
        public DateTime? SessionStartedDateTimeUtc { get; }

        /// <summary>
        /// How long a session is allowed to exist
        /// </summary>
        public long SessionExpirationInMinutes { get; }

        /// <summary>
        /// What chunk size to use
        /// </summary>
        public long ChunkSizeInBytes { get; set; }
    }
}
