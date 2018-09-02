namespace Fabric.Metadata.FileService.Client.Events
{
    using System;

    /// <summary>
    /// Information about a file checked with the server
    /// </summary>
    public class FileCheckedEventArgs : EventArgs
    {
        public FileCheckedEventArgs(int resourceId, bool wasFileFound, string hashForFile, string hashForFileOnServer,
            DateTimeOffset? headersLastModified, string contentDispositionFileName, bool didHashMatch)
        {
            ResourceId = resourceId;
            this.WasFileFound = wasFileFound;
            this.HashForLocalFile = hashForFile;
            this.HashOnServer = hashForFileOnServer;
            this.LastUploadedToServer = headersLastModified;
            this.FileNameOnServer = contentDispositionFileName;
            this.DidHashMatch = didHashMatch;
        }

        /// <summary>
        /// Did the stored has of the file on the server match the calculated hash of the local file
        /// </summary>
        public bool DidHashMatch { get; set; }

        public string FileNameOnServer { get; set; }

        /// <summary>
        /// When was the file last uploaded to the server
        /// </summary>
        public DateTimeOffset? LastUploadedToServer { get; set; }

        public string HashOnServer { get; set; }

        public string HashForLocalFile { get; set; }
        public int ResourceId { get; }

        /// <summary>
        /// Was the file present on the server?
        /// </summary>
        public bool WasFileFound { get; set; }
    }
}