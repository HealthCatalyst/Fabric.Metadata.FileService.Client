using System;
using System.Collections.Generic;
using System.Text;

namespace Fabric.Metadata.FileService.Client
{
    using System.Net.Http;
    using Interfaces;

    public class FileServiceClientFactory : IFileServiceClientFactory
    {
        public IFileServiceClient CreateFileServiceClient(IAccessTokenRepository accessTokenRepository, string mdsBaseUrl)
        {
            return new FileServiceClient(accessTokenRepository, mdsBaseUrl, new HttpClientHandler());
        }
    }
}
