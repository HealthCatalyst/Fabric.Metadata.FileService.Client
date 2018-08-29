namespace Fabric.Metadata.FileService.Client.Events
{
    using System;

    public class FileCheckedEventArgs : EventArgs
    {
        public FileCheckedEventArgs(int resourceId, bool wasFileFound, string hashForFile, string hashForFileOnServer,
            DateTimeOffset? headersLastModified, string contentDispositionFileName, bool didHashMatch)
        {
            ResourceId = resourceId;
            this.WasFileFound = wasFileFound;
            this.HashForLocalFile = hashForFile;
            this.HashOnServer = hashForFileOnServer;
            this.LastModifiedOnServer = headersLastModified;
            this.FileNameOnServer = contentDispositionFileName;
            this.DidHashMatch = didHashMatch;
        }

        public bool DidHashMatch { get; set; }

        public string FileNameOnServer { get; set; }

        public DateTimeOffset? LastModifiedOnServer { get; set; }

        public string HashOnServer { get; set; }

        public string HashForLocalFile { get; set; }
        public int ResourceId { get; }
        public bool WasFileFound { get; set; }
    }
}