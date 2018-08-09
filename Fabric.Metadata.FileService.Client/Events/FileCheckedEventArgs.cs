using System;

namespace Fabric.Metadata.FileService.Client.Events
{
    public class FileCheckedEventArgs : EventArgs
    {
        public FileCheckedEventArgs(bool fileFound, string hashForFile, string hashForFileOnServer,
            DateTimeOffset? headersLastModified, string contentDispositionFileName, bool hashMatches)
        {
            this.FileFound = fileFound;
            this.HashForLocalFile = hashForFile;
            this.HashOnServer = hashForFileOnServer;
            this.LastModifiedOnServer = headersLastModified;
            this.FileNameOnServer = contentDispositionFileName;
            this.HashMatches = hashMatches;
        }

        public bool HashMatches { get; set; }

        public string FileNameOnServer { get; set; }

        public DateTimeOffset? LastModifiedOnServer { get; set; }

        public string HashOnServer { get; set; }

        public string HashForLocalFile { get; set; }

        public bool FileFound { get; set; }
    }
}