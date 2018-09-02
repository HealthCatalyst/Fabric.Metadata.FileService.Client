namespace Fabric.Metadata.FileService.Client.Interfaces
{
    public interface IFileServiceClientFactory
    {
        IFileServiceClient CreateFileServiceClient(IAccessTokenRepository accessTokenRepository, string mdsBaseUrl);
    }
}