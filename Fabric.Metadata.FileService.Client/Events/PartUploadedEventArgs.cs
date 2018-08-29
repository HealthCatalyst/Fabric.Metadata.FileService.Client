namespace Fabric.Metadata.FileService.Client.Events
{
    using System;
    using System.ComponentModel;
    using Structures;

    public class PartUploadedEventArgs : CancelEventArgs
    {
        public PartUploadedEventArgs(int resourceId, Guid sessionId, string fileName, FilePart filePart,
            string statusCode, int totalFileParts,
            int numPartsUploaded)
        {
            this.FileName = fileName;
            this.FilePart = filePart;
            this.StatusCode = statusCode;
            this.TotalFileParts = totalFileParts;
            this.NumPartsUploaded = numPartsUploaded;
            SessionId = sessionId;
            ResourceId = resourceId;
        }

        public string FileName { get; }

        public int TotalFileParts { get; }

        public string StatusCode { get; }

        public FilePart FilePart { get; }

        public int NumPartsUploaded { get; }
        public Guid SessionId { get; }
        public int ResourceId { get; }
    }
}