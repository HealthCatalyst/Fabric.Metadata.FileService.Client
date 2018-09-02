namespace Fabric.Metadata.FileService.Client.Events
{
    using System;

    public class TransientErrorEventArgs : EventArgs
    {
        public TransientErrorEventArgs(int resourceId, string method, Uri fullUri, string statusCode,
            string response)
        {
            this.Method = method;
            this.FullUri = fullUri;
            this.StatusCode = statusCode;
            this.Response = response;
            ResourceId = resourceId;
        }

        public string Method { get; }
        public Uri FullUri { get; }
        public string StatusCode { get; }
        public string Response { get; }
        public int ResourceId { get; }
    }
}