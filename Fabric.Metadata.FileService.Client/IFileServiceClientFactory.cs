namespace Fabric.Metadata.FileService.Client
{
    public interface IFileServiceClientFactory
    {
        IFileServiceClient CreateFileServiceClient(string accessToken, string mdsBaseUrl);
    }
}