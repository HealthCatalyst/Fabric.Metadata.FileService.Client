namespace Fabric.Metadata.FileService.Client.Interfaces
{
    using System.Threading.Tasks;

    public interface IFileServiceAccessTokenRepository
    {
        /// <summary>
        /// Gets a valid access token
        /// </summary>
        /// <returns></returns>
        Task<string> GetAccessTokenAsync();

        /// <summary>
        /// Gets a brand new access token.  Likely because the token received via GetAccessTokenAsync() was not accepted by the server
        /// </summary>
        /// <returns></returns>
        Task<string> GetNewAccessTokenAsync();
    }
}
