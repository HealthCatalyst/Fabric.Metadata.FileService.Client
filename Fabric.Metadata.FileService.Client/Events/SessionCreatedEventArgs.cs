namespace Fabric.Metadata.FileService.Client.Events
{
    using System;
    using System.ComponentModel;

    public class SessionCreatedEventArgs : CancelEventArgs
    {
        public SessionCreatedEventArgs(Guid sessionId, long chunkSizeInBytes, long maxFileSizeInMegabytes)
        {
            this.SessionId = sessionId;
            this.ChunkSizeInBytes = chunkSizeInBytes;
            this.MaxFileSizeInMegabytes = maxFileSizeInMegabytes;
        }

        public long MaxFileSizeInMegabytes { get; set; }

        public long ChunkSizeInBytes { get; set; }

        public Guid SessionId { get; set; }
    }
}
