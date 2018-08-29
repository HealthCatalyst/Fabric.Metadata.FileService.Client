namespace Fabric.Metadata.FileService.Client.FileServiceResults
{
    using Structures;

    public class CreateSessionResult : FileServiceResult
    {
        public UploadSession Session { get; set; }
    }
}