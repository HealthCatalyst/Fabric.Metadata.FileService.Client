using System;
using System.Collections.Generic;
using System.Text;

namespace Fabric.Metadata.FileService.Client.Interfaces
{
    using System.Threading.Tasks;

    public interface IAccessTokenRepository
    {
        Task<string> GetAccessTokenAsync();
    }
}
