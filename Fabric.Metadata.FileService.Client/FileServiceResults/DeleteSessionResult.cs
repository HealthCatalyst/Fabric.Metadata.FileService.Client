namespace Fabric.Metadata.FileService.Client.FileServiceResults
{
    using Structures;

    public class DeleteSessionResult : FileServiceResult
    {
        public UploadSession Session { get; set; }
    }
}