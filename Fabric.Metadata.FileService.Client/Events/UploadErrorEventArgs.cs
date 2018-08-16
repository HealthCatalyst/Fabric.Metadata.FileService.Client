namespace Fabric.Metadata.FileService.Client.Events
{
    using System;
    using System.ComponentModel;

    public class UploadErrorEventArgs : CancelEventArgs
    {
        public UploadErrorEventArgs(Uri fullUri, string statusCode, string content, int resourceId)
        {
            this.FullUri = fullUri;
            this.StatusCode = statusCode;
            this.Response = content;
            ResourceId = resourceId;
        }

        public string Response { get; set; }
        public int ResourceId { get; }
        public string StatusCode { get; set; }

        public Uri FullUri { get; set; }
    }
}