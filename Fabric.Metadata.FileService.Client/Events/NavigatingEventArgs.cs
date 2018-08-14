namespace Fabric.Metadata.FileService.Client.Events
{
    using System;
    using System.ComponentModel;

    public class NavigatingEventArgs : CancelEventArgs
    {
        public NavigatingEventArgs(Uri fullUri, string method)
        {
            this.FullUri = fullUri;
            this.Method = method;
        }

        public string Method { get; set; }

        public Uri FullUri { get; set; }
    }
}