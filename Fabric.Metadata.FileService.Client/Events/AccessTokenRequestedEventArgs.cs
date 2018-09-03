namespace Fabric.Metadata.FileService.Client.Events
{
    using System;

    public class AccessTokenRequestedEventArgs : EventArgs
    {
        public AccessTokenRequestedEventArgs(int resourceId)
        {
            ResourceId = resourceId;
        }

        public int ResourceId { get; }
    }
}