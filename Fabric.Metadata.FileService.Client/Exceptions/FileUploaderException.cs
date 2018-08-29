namespace Fabric.Metadata.FileService.Client.Exceptions
{
    using System;

    [Serializable]
    public class FileUploaderException : Exception
    {
        public FileUploaderException(Uri fullUri, string statusCode, string errorContent)
        : base($"Error: {fullUri} returned status code {statusCode} and error {errorContent}")
        {
            FullUri = fullUri;
            StatusCode = statusCode;
            ErrorContent = errorContent;
        }

        public Uri FullUri { get; }
        public string StatusCode { get; }
        public string ErrorContent { get; }
    }
}
