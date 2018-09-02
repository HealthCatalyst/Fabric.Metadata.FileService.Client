namespace Fabric.Metadata.FileService.Client.Interfaces
{
    using System;

    public interface IFileServiceClientFactory
    {
        IFileServiceClient CreateFileServiceClient(IAccessTokenRepository accessTokenRepository, Uri mdsBaseUrl);
    }
}