namespace Fabric.Metadata.FileService.Client.Events
{
    using System.ComponentModel;

    public class PartUploadedEventArgs : CancelEventArgs
    {
        public PartUploadedEventArgs(string fileName, FilePart filePart, string statusCode, int filePartsCount,
            int numPartsUploaded)
        {
            this.FileName = fileName;
            this.FilePart = filePart;
            this.StatusCode = statusCode;
            this.FilePartsCount = filePartsCount;
            this.NumPartsUploaded = numPartsUploaded;
        }

        public string FileName { get; }

        public int FilePartsCount { get; }

        public string StatusCode { get; }

        public FilePart FilePart { get; }

        public int NumPartsUploaded { get; }
    }
}