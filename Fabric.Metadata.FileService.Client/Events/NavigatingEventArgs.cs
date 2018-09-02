namespace Fabric.Metadata.FileService.Client.Events
{
    using System;
    using System.ComponentModel;

    public class NavigatingEventArgs : CancelEventArgs
    {
        public NavigatingEventArgs(int resourceId, string method, Uri fullUri)
        {
            this.FullUri = fullUri;
            this.Method = method;
            ResourceId = resourceId;
        }

        public string Method { get; set; }
        public int ResourceId { get; }
        public Uri FullUri { get; set; }
    }
}