namespace fabric.metadata.fileserver.client.console
{
    using System;
    using System.Threading.Tasks;

    using Fabric.Metadata.FileService.Client.Interfaces;

    class AccessTokenRepository : IAccessTokenRepository
    {
        private readonly string accessToken;

        public AccessTokenRepository(string accessToken)
        {
            if (string.IsNullOrWhiteSpace(accessToken))
            {
                throw new ArgumentException("message", nameof(accessToken));
            }

            this.accessToken = accessToken;
        }

        public Task<string> GetAccessTokenAsync()
        {
            return Task.FromResult(accessToken);
        }
    }
}
