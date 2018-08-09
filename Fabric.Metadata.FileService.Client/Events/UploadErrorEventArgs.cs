namespace Fabric.Metadata.FileService.Client.Events
{
    using System;
    using System.ComponentModel;

    public class UploadErrorEventArgs : CancelEventArgs
    {
        public UploadErrorEventArgs(Uri fullUri, string statusCode, string content)
        {
            this.FullUri = fullUri;
            this.StatusCode = statusCode;
            this.Response = content;
        }

        public string Response { get; set; }

        public string StatusCode { get; set; }

        public Uri FullUri { get; set; }
    }
}