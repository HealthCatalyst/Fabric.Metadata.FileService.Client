namespace Fabric.Metadata.FileService.Client.FileServiceResults
{
    using System;
    using System.Net;

    public class FileServiceResult
    {
        public HttpStatusCode StatusCode { get; set; }
        public string Error { get; set; }
        public Uri FullUri { get; set; }
        public string ErrorCode { get; set; }
    }
}