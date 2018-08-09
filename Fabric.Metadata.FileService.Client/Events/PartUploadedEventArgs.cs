namespace Fabric.Metadata.FileService.Client.Events
{
    using System.ComponentModel;

    public class PartUploadedEventArgs : CancelEventArgs
    {
        public PartUploadedEventArgs(string fileName, FilePart filePart, string statusCode, int filePartsCount)
        {
            this.FileName = fileName;
            this.FilePart = filePart;
            this.StatusCode = statusCode;
            this.FilePartsCount = filePartsCount;
        }

        public string FileName { get; set; }

        public int FilePartsCount { get; set; }

        public string StatusCode { get; set; }

        public FilePart FilePart { get; set; }
    }
}