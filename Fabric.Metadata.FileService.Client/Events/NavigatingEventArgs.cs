namespace Fabric.Metadata.FileService.Client.Events
{
    using System;
    using System.ComponentModel;

    public class NavigatingEventArgs : CancelEventArgs
    {
        public NavigatingEventArgs(Uri fullUri, string method)
        {
            this.Uri = fullUri;
            this.Method = method;
        }

        public string Method { get; set; }

        public Uri Uri { get; set; }
    }
}