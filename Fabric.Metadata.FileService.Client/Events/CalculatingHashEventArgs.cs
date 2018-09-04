namespace Fabric.Metadata.FileService.Client.Events
{
    using System.ComponentModel;

    public class CalculatingHashEventArgs : CancelEventArgs
    {
        public CalculatingHashEventArgs(int resourceId, string filePath, long fileSize)
        {
            ResourceId = resourceId;
            FilePath = filePath;
            FileSize = fileSize;
        }

        public int ResourceId { get; }
        public string FilePath { get; }
        public long FileSize { get; }
    }
}