namespace Fabric.Metadata.FileService.Client.Exceptions
{
    using System;

    [Serializable]
    public class InvalidAccessTokenException : Exception
    {
        public InvalidAccessTokenException(string accessToken)
            : base($"Received invalid Access Token from AccessTokenRepository '{accessToken}'")
        {
        }
    }
}