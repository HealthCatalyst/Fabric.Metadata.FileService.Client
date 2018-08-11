namespace Fabric.Metadata.FileService.Client.Events
{
    using System.ComponentModel;

    public class FileUploadCompletedEventArgs : CancelEventArgs
    {
        public FileUploadCompletedEventArgs(string filename)
        {
            this.FileName = filename;
        }

        public string FileName { get; set; }
    }
}