namespace Fabric.Metadata.FileService.Client.Events
{
    using System;
    using System.Collections.Generic;
    using Structures;

    public class CommittingEventArgs : EventArgs
    {
        public CommittingEventArgs(int resourceId, Guid uploadSessionSessionId, string fileName, string hashForFile, long fullFileSize, IList<FilePart> fileParts)
        {
            ResourceId = resourceId;
            UploadSessionSessionId = uploadSessionSessionId;
            FileName = fileName;
            HashForFile = hashForFile;
            FullFileSize = fullFileSize;
            FileParts = fileParts;
        }

        public int ResourceId { get; }
        public Guid UploadSessionSessionId { get; }
        public string FileName { get; }
        public string HashForFile { get; }
        public long FullFileSize { get; }
        public IList<FilePart> FileParts { get; }
    }
}