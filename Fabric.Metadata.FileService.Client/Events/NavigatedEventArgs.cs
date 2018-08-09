namespace Fabric.Metadata.FileService.Client.Events
{
    using System;

    public class NavigatedEventArgs : EventArgs
    {
        public NavigatedEventArgs(string method, Uri fullUri, string statusCode)
        {
            this.Method = method;
            this.FullUri = fullUri;
            this.StatusCode = statusCode;
        }

        public string Method { get; set; }

        public string StatusCode { get; set; }

        public Uri FullUri { get; set; }
    }
}