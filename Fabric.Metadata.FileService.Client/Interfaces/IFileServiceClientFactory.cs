namespace Fabric.Metadata.FileService.Client.Interfaces
{
    using System;
    using System.Threading;

    public interface IFileServiceClientFactory
    {
        IFileServiceClient CreateFileServiceClient(IFileServiceAccessTokenRepository fileServiceAccessTokenRepository,
            Uri mdsBaseUrl, CancellationToken cancellationToken);
    }
}