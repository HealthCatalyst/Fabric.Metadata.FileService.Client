namespace Fabric.Metadata.FileService.Client.Events
{
    using System.ComponentModel;

    public class FileUploadedEventArgs : CancelEventArgs
    {
        public FileUploadedEventArgs(string filename)
        {
            this.FileName = filename;
        }

        public string FileName { get; set; }
    }
}