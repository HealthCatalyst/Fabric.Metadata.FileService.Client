namespace Fabric.Metadata.FileService.Client.FileServiceResults
{
    using System;

    public class CheckFileResult : FileServiceResult
    {
        public DateTimeOffset? LastModified { get; set; }
        public string FileNameOnServer { get; set; }
        public string HashForFileOnServer { get; set; }
    }
}