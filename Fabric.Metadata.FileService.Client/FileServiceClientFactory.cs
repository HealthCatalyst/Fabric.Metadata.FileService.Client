using System;
using System.Collections.Generic;
using System.Text;

namespace Fabric.Metadata.FileService.Client
{
    using System.Net.Http;
    using Interfaces;

    public class FileServiceClientFactory : IFileServiceClientFactory
    {
        public IFileServiceClient CreateFileServiceClient(string accessToken, string mdsBaseUrl)
        {
            return new FileServiceClient(accessToken, mdsBaseUrl, new HttpClientHandler());
        }
    }
}
