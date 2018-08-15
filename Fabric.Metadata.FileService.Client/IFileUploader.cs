namespace Fabric.Metadata.FileService.Client
{
    using System;
    using System.Threading.Tasks;

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
        Task UploadFileAsync(string filePath, string accessToken, int resourceId, string mdsBaseUrl);
        Task DownloadFileAsync(string accessToken, int resourceId, string utTempFolder, string mdsBaseUrl);
    }
}