namespace Fabric.Metadata.FileService.Client.Interfaces
{
    public interface IFileServiceClientFactory
    {
        IFileServiceClient CreateFileServiceClient(string accessToken, string mdsBaseUrl);
    }
}