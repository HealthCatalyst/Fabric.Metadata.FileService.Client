namespace Fabric.Metadata.FileService.Client.Events
{
    using System;

    public class TransientErrorEventArgs : EventArgs
    {
        public TransientErrorEventArgs(string method, Uri fullUri, string resultStatusCode, string content)
        {
            this.Method = method;
            this.FullUri = fullUri;
            this.ResultStatusCode = resultStatusCode;
            this.Content = content;
        }

        public string Method { get; }
        public Uri FullUri { get; }
        public string ResultStatusCode { get; }
        public string Content { get; }
    }
}