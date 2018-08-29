namespace Fabric.Metadata.FileService.Client.FileServiceResults
{
    using Structures;

    public class CommitResult : FileServiceResult
    {
        public UploadSession Session { get; set; }
    }
}