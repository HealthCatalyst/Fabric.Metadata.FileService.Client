namespace Fabric.Metadata.FileService.Client.Events
{
    using System;
    using System.ComponentModel;

    public class FileUploadCompletedEventArgs : CancelEventArgs
    {
        public FileUploadCompletedEventArgs(int resourceId, Guid sessionId, string filename, 
            string fileHash, DateTime? sessionStartedDateTimeUtc, DateTime? sessionFinishedDateTimeUtc, 
            string sessionStartedBy)
        {
            ResourceId = resourceId;
            SessionId = sessionId;
            this.FileName = filename;
            FileHash = fileHash;
            SessionStartedDateTimeUtc = sessionStartedDateTimeUtc;
            SessionFinishedDateTimeUtc = sessionFinishedDateTimeUtc;
            SessionStartedBy = sessionStartedBy;
        }

        public int ResourceId { get; }
        public Guid SessionId { get; }
        public string FileName { get; set; }
        public string FileHash { get; }
        public DateTime? SessionStartedDateTimeUtc { get; }
        public DateTime? SessionFinishedDateTimeUtc { get; }
        public string SessionStartedBy { get; }
    }
}