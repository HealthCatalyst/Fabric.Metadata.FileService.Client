namespace Fabric.Metadata.FileService.Client.Events
{
    using System;
    using System.ComponentModel;

    public class SessionCreatedEventArgs : CancelEventArgs
    {

        public SessionCreatedEventArgs(int resourceId, Guid sessionId, long chunkSizeInBytes, long maxFileSizeInMegabytes, 
            string sessionStartedBy, DateTime? sessionStartedDateTimeUtc, 
            int sessionExpirationInMinutes)
        {
            this.SessionId = sessionId;
            this.ChunkSizeInBytes = chunkSizeInBytes;
            this.MaxFileSizeInMegabytes = maxFileSizeInMegabytes;
            SessionStartedBy = sessionStartedBy;
            SessionStartedDateTimeUtc = sessionStartedDateTimeUtc;
            SessionExpirationInMinutes = sessionExpirationInMinutes;
            this.ResourceId = resourceId;
        }

        public long MaxFileSizeInMegabytes { get; set; }
        public string SessionStartedBy { get; }
        public DateTime? SessionStartedDateTimeUtc { get; }
        public int SessionExpirationInMinutes { get; }
        public int ResourceId { get; }
        public long ChunkSizeInBytes { get; set; }

        public Guid SessionId { get; set; }
    }
}
