namespace Fabric.Metadata.FileService.Client.Events
{
    using System;

    public class NavigatedEventArgs : EventArgs
    {
        public NavigatedEventArgs(int resourceId, string method, Uri fullUri, string statusCode)
        {
            this.Method = method;
            this.FullUri = fullUri;
            this.StatusCode = statusCode;
            ResourceId = resourceId;
        }

        public string Method { get; set; }

        public string StatusCode { get; set; }
        public int ResourceId { get; }
        public Uri FullUri { get; set; }
    }
}