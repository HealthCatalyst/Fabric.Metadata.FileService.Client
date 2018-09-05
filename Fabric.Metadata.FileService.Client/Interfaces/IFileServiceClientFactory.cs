namespace Fabric.Metadata.FileService.Client.Interfaces
{
    using System;

    public interface IFileServiceClientFactory
    {
        IFileServiceClient CreateFileServiceClient(IFileServiceAccessTokenRepository fileServiceAccessTokenRepository, Uri mdsBaseUrl);
    }
}