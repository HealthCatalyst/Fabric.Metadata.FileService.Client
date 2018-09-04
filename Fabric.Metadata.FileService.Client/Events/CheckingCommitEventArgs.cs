namespace Fabric.Metadata.FileService.Client.Events
{
    using System;

    public class CheckingCommitEventArgs : EventArgs
    {
        public CheckingCommitEventArgs(int resourceId, Guid sessionId, int timesCalled)
        {
            ResourceId = resourceId;
            SessionId = sessionId;
            TimesCalled = timesCalled;
        }

        public int ResourceId { get; }
        public Guid SessionId { get; }
        public int TimesCalled { get; }
    }
}