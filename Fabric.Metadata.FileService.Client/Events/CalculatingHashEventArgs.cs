namespace Fabric.Metadata.FileService.Client.Events
{
    using System.ComponentModel;

    public class CalculatingHashEventArgs : CancelEventArgs
    {
        public CalculatingHashEventArgs(int resourceId, string filePath)
        {
            ResourceId = resourceId;
            FilePath = filePath;
        }

        public int ResourceId { get; }
        public string FilePath { get; }
    }
}