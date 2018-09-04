namespace Fabric.Metadata.FileService.Client.Interfaces
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Events;

    public interface IFileUploader : IDisposable
    {
        event NavigatingEventHandler Navigating;
        event NavigatedEventHandler Navigated;
        event PartUploadedEventHandler PartUploaded;
        event FileUploadStartedEventHandler FileUploadStarted;
        event FileUploadCompletedEventHandler FileUploadCompleted;
        event UploadErrorEventHandler UploadError;
        event SessionCreatedEventHandler SessionCreated;
        event FileCheckedEventHandler FileChecked;
        event TransientErrorEventHandler TransientError;
        event AccessTokenRequestedEventHandler AccessTokenRequested;
        event NewAccessTokenRequestedEventHandler NewAccessTokenRequested;
        event CalculatingHashEventHandler CalculatingHash;
        event CommittingEventHandler Committing;
        event CheckingCommitEventHandler CheckingCommit;

        Task UploadFileAsync(int resourceId, string filePath, CancellationToken ctsToken);
        Task DownloadFileAsync(int resourceId, string utTempFolder, CancellationToken ctsToken);

    }
}