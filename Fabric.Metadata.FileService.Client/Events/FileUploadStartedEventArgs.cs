using System.ComponentModel;

namespace Fabric.Metadata.FileService.Client.Events
{
    public class FileUploadStartedEventArgs : CancelEventArgs
    {
        public FileUploadStartedEventArgs(string filename, int filePartsCount)
        {
            this.FileName = filename;
            FilePartsCount = filePartsCount;
        }

        public string FileName { get; set; }
        public int FilePartsCount { get; }
    }
}