namespace Fabric.Metadata.FileService.Client
{
    using System;

    using System.Net.Http;
    using System.Threading;
    using Interfaces;

    public class FileServiceClientFactory : IFileServiceClientFactory
    {
        public IFileServiceClient CreateFileServiceClient(
            IFileServiceAccessTokenRepository fileServiceAccessTokenRepository, Uri mdsBaseUrl,
            CancellationToken cancellationToken)
        {
            return new FileServiceClient(fileServiceAccessTokenRepository, mdsBaseUrl, new HttpClientHandler(), cancellationToken);
        }
    }
}
