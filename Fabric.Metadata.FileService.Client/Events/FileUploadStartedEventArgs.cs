using System.ComponentModel;

namespace Fabric.Metadata.FileService.Client.Events
{
    using System;

    public class FileUploadStartedEventArgs : CancelEventArgs
    {
        public FileUploadStartedEventArgs(int resourceId, Guid sessionId,
            string filename, int totalFileParts)
        {
            ResourceId = resourceId;
            SessionId = sessionId;
            this.FileName = filename;
            TotalFileParts = totalFileParts;
        }

        public int ResourceId { get; }
        public Guid SessionId { get; }
        public string FileName { get; set; }
        public int TotalFileParts { get; }
    }
}