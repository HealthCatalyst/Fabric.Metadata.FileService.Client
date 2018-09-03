namespace Fabric.Metadata.FileService.Client.Events
{
    using System;

    public class NewAccessTokenRequestedEventArgs : EventArgs
    {
        public NewAccessTokenRequestedEventArgs(int resourceId)
        {
            ResourceId = resourceId;
        }

        public int ResourceId { get; }
    }
}