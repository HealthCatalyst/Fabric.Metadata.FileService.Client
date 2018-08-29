namespace Fabric.Metadata.FileService.Client
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Threading.Tasks;
    using System.Net;
    using Events;

    public delegate void NavigatingEventHandler(object sender, NavigatingEventArgs e);
    public delegate void NavigatedEventHandler(object sender, NavigatedEventArgs e);
    public delegate void PartUploadedEventHandler(object sender, PartUploadedEventArgs e);
    public delegate void FileUploadStartedEventHandler(object sender, FileUploadStartedEventArgs e);
    public delegate void FileUploadCompletedEventHandler(object sender, FileUploadCompletedEventArgs e);
    public delegate void UploadErrorEventHandler(object sender, UploadErrorEventArgs e);
    public delegate void SessionCreatedEventHandler(object sender, SessionCreatedEventArgs e);
    public delegate void FileCheckedEventHandler(object sender, FileCheckedEventArgs e);

    public class FileUploader : IFileUploader
    {
        public event NavigatingEventHandler Navigating;
        public event NavigatedEventHandler Navigated;
        public event PartUploadedEventHandler PartUploaded;
        public event FileUploadStartedEventHandler FileUploadStarted;
        public event FileUploadCompletedEventHandler FileUploadCompleted;
        public event UploadErrorEventHandler UploadError;
        public event SessionCreatedEventHandler SessionCreated;
        public event FileCheckedEventHandler FileChecked;

        private int numPartsUploaded = 0;
        private readonly IFileServiceClientFactory fileServiceClientFactory;

        public FileUploader()
            : this(new FileServiceClientFactory())
        {
        }

        public FileUploader(IFileServiceClientFactory fileServiceClientFactory)
        {
            this.fileServiceClientFactory = fileServiceClientFactory;
        }

        public async Task UploadFileAsync(string filePath, string accessToken, int resourceId, string mdsBaseUrl)
        {
            if (string.IsNullOrWhiteSpace(filePath)) throw new ArgumentNullException(nameof(filePath));
            if (string.IsNullOrWhiteSpace(accessToken)) throw new ArgumentNullException(nameof(accessToken));
            if (string.IsNullOrWhiteSpace(mdsBaseUrl)) throw new ArgumentNullException(nameof(mdsBaseUrl));
            if (resourceId <= 0) throw new ArgumentOutOfRangeException(nameof(resourceId));

            if (!mdsBaseUrl.EndsWith(@"/"))
            {
                mdsBaseUrl += @"/";
            }

            var fileSplitter = new FileSplitter();
            var fileName = Path.GetFileName(filePath);

            var hashForFile = new MD5FileHasher().CalculateHashForFile(filePath);

            // first check if the server already has the file
            bool fileAlreadyExists = await CheckIfFileExistsOnServerAsync(mdsBaseUrl, accessToken, resourceId, fileName, filePath, hashForFile);

            if (fileAlreadyExists) return;

            // create new upload session
            var uploadSession = await CreateNewUploadSessionAsync(mdsBaseUrl, accessToken, resourceId);

            numPartsUploaded = 0;

            var fullFileSize = new FileInfo(filePath).Length;

            var countOfFileParts = fileSplitter.GetCountOfFileParts(uploadSession.FileUploadChunkSizeInBytes, fullFileSize);

            OnFileUploadStarted(new FileUploadStartedEventArgs(resourceId, uploadSession.SessionId, fileName, countOfFileParts));

            var fileParts = await fileSplitter.SplitFile(filePath, fileName, uploadSession.FileUploadChunkSizeInBytes,
                uploadSession.FileUploadMaxFileSizeInMegabytes,
                (stream, part) => UploadFilePartStreamAsync(stream, part, mdsBaseUrl, accessToken, resourceId, uploadSession.SessionId, fileName, fullFileSize, countOfFileParts));

            await CommitAsync(mdsBaseUrl, accessToken, resourceId, uploadSession.SessionId, fileName, hashForFile,
                fullFileSize, fileParts);
        }

        public async Task DownloadFileAsync(string accessToken, int resourceId, string utTempFolder, string mdsBaseUrl)
        {
            if (mdsBaseUrl == null) throw new ArgumentNullException(nameof(mdsBaseUrl));
            if (accessToken == null) throw new ArgumentNullException(nameof(accessToken));
            if (resourceId <= 0) throw new ArgumentOutOfRangeException(nameof(resourceId));

            using (var fileServiceClient = CreateFileServiceClient(accessToken, mdsBaseUrl))
            {
                fileServiceClient.Navigating += OnNavigatingRelay;
                fileServiceClient.Navigated += OnNavigatedRelay;

                try
                {
                    var result = await fileServiceClient.DownloadFileAsync(resourceId, utTempFolder);

                    if (result.StatusCode == HttpStatusCode.OK)
                    {

                    }
                    else
                    {
                        OnUploadError(new UploadErrorEventArgs(result.FullUri, result.StatusCode.ToString(), result.Error, resourceId));
                    }

                }
                finally
                {
                    fileServiceClient.Navigating -= OnNavigatingRelay;
                    fileServiceClient.Navigated -= OnNavigatedRelay;
                }
            }
        }

        private async Task<bool> CheckIfFileExistsOnServerAsync(string mdsBaseUrl, string accessToken, int resourceId,
            string fileName, string filePath, string hashForFile)
        {
            if (mdsBaseUrl == null) throw new ArgumentNullException(nameof(mdsBaseUrl));
            if (accessToken == null) throw new ArgumentNullException(nameof(accessToken));
            if (resourceId <= 0) throw new ArgumentOutOfRangeException(nameof(resourceId));

            using (var fileServiceClient = CreateFileServiceClient(accessToken, mdsBaseUrl))
            {
                fileServiceClient.Navigating += OnNavigatingRelay;
                fileServiceClient.Navigated += OnNavigatedRelay;

                try
                {
                    var result = await fileServiceClient.CheckFileAsync(resourceId);

                    switch (result.StatusCode)
                    {
                        case HttpStatusCode.NoContent:
                            {
                                if (result.HashForFileOnServer != null)
                                {
                                    OnFileChecked(new FileCheckedEventArgs(resourceId, true, hashForFile,
                                        result.HashForFileOnServer, result.LastModified,
                                        result.FileNameOnServer, hashForFile == result.HashForFileOnServer));

                                    if (hashForFile == result.HashForFileOnServer
                                        && Path.GetFileName(filePath) == Path.GetFileName(result.FileNameOnServer))
                                    {
                                        return true;
                                    }
                                }

                                break;
                            }
                        case HttpStatusCode.NotFound:
                            // this is acceptable response if the file does not exist on the server
                            break;

                        default:
                            OnUploadError(new UploadErrorEventArgs(result.FullUri, result.StatusCode.ToString(), result.Error, resourceId));
                            throw new Exception("Error" + result.StatusCode.ToString());
                    }
                }
                finally
                {
                    fileServiceClient.Navigating -= OnNavigatingRelay;
                    fileServiceClient.Navigated -= OnNavigatedRelay;
                }
            }

            return false;
        }

        private async Task<UploadSession> CreateNewUploadSessionAsync(string mdsBaseUrl, string accessToken, int resourceId)
        {
            if (mdsBaseUrl == null) throw new ArgumentNullException(nameof(mdsBaseUrl));
            if (accessToken == null) throw new ArgumentNullException(nameof(accessToken));
            if (resourceId <= 0) throw new ArgumentOutOfRangeException(nameof(resourceId));

            using (var fileServiceClient = CreateFileServiceClient(accessToken, mdsBaseUrl))
            {
                fileServiceClient.Navigating += OnNavigatingRelay;
                fileServiceClient.Navigated += OnNavigatedRelay;

                try
                {
                    var result = await fileServiceClient.CreateNewUploadSessionAsync(resourceId);

                    switch (result.StatusCode)
                    {
                        case HttpStatusCode.OK:
                            OnSessionCreated(new SessionCreatedEventArgs(
                                resourceId, result.Session.SessionId, result.Session.FileUploadChunkSizeInBytes,
                                result.Session.FileUploadMaxFileSizeInMegabytes,
                                result.Session.SessionStartedBy, result.Session.SessionStartedDateTimeUtc,
                                result.Session.FileUploadSessionExpirationInMinutes));

                            return result.Session;

                        default:
                            OnUploadError(new UploadErrorEventArgs(result.FullUri, result.StatusCode.ToString(), result.Error, resourceId));
                            throw new Exception("Error" + result.StatusCode.ToString());
                    }
                }
                finally
                {
                    fileServiceClient.Navigating -= OnNavigatingRelay;
                    fileServiceClient.Navigated -= OnNavigatedRelay;
                }
            }
        }

        private async Task<UploadSession> DeleteUploadSessionAsync(string mdsBaseUrl, string accessToken, int resourceId)
        {
            if (mdsBaseUrl == null) throw new ArgumentNullException(nameof(mdsBaseUrl));
            if (accessToken == null) throw new ArgumentNullException(nameof(accessToken));
            if (resourceId <= 0) throw new ArgumentOutOfRangeException(nameof(resourceId));

            using (var fileServiceClient = CreateFileServiceClient(accessToken, mdsBaseUrl))
            {
                fileServiceClient.Navigating += OnNavigatingRelay;
                fileServiceClient.Navigated += OnNavigatedRelay;

                try
                {
                    var result = await fileServiceClient.DeleteUploadSessionAsync(resourceId);

                    switch (result.StatusCode)
                    {
                        case HttpStatusCode.OK:
                            return result.Session;
                        default:
                            OnUploadError(new UploadErrorEventArgs(result.FullUri, result.StatusCode.ToString(), result.Error, resourceId));
                            throw new Exception("Error" + result.StatusCode.ToString());
                    }
                }
                finally
                {
                    fileServiceClient.Navigating -= OnNavigatingRelay;
                    fileServiceClient.Navigated -= OnNavigatedRelay;
                }
            }
        }


        private async Task UploadFilePartStreamAsync(Stream stream, FilePart filePart, string mdsBaseUrl,
            string accessToken,
            int resourceId,
            Guid sessionId, string fileName, long fullFileSize, int filePartsCount)
        {
            if (filePart == null) throw new ArgumentNullException(nameof(filePart));
            if (mdsBaseUrl == null) throw new ArgumentNullException(nameof(mdsBaseUrl));
            if (accessToken == null) throw new ArgumentNullException(nameof(accessToken));
            if (fileName == null) throw new ArgumentNullException(nameof(fileName));
            if (resourceId <= 0) throw new ArgumentOutOfRangeException(nameof(resourceId));
            if (fullFileSize <= 0) throw new ArgumentOutOfRangeException(nameof(fullFileSize));
            if (filePartsCount <= 0) throw new ArgumentOutOfRangeException(nameof(filePartsCount));
            if (sessionId == default(Guid)) throw new ArgumentOutOfRangeException(nameof(sessionId));


            using (var fileServiceClient = CreateFileServiceClient(accessToken, mdsBaseUrl))
            {
                fileServiceClient.Navigating += OnNavigatingRelay;
                fileServiceClient.Navigated += OnNavigatedRelay;

                try
                {
                    var result = await fileServiceClient.UploadStreamAsync(resourceId, sessionId, stream, filePart, fileName, fullFileSize, filePartsCount, numPartsUploaded);

                    switch (result.StatusCode)
                    {
                        case HttpStatusCode.OK:
                            OnPartUploaded(
                                new PartUploadedEventArgs(resourceId, sessionId, fileName, filePart, result.StatusCode.ToString(), filePartsCount, result.PartsUploaded));

                            numPartsUploaded++;
                            break;

                        default:
                            OnUploadError(new UploadErrorEventArgs(result.FullUri, result.StatusCode.ToString(), result.Error, resourceId));
                            throw new Exception("Error" + result.StatusCode.ToString());
                    }
                }
                finally
                {
                    fileServiceClient.Navigating -= OnNavigatingRelay;
                    fileServiceClient.Navigated -= OnNavigatedRelay;
                }
            }
        }

        private async Task CommitAsync(string mdsBaseUrl, string accessToken, int resourceId, Guid sessionId,
            string filename, string fileHash, long fileSize, IList<FilePart> utFileParts)
        {
            if (mdsBaseUrl == null) throw new ArgumentNullException(nameof(mdsBaseUrl));
            if (accessToken == null) throw new ArgumentNullException(nameof(accessToken));
            if (filename == null) throw new ArgumentNullException(nameof(filename));
            if (fileHash == null) throw new ArgumentNullException(nameof(fileHash));
            if (utFileParts == null) throw new ArgumentNullException(nameof(utFileParts));
            if (resourceId <= 0) throw new ArgumentOutOfRangeException(nameof(resourceId));
            if (fileSize <= 0) throw new ArgumentOutOfRangeException(nameof(fileSize));
            if (sessionId == default(Guid)) throw new ArgumentOutOfRangeException(nameof(sessionId));


            using (var fileServiceClient = CreateFileServiceClient(accessToken, mdsBaseUrl))
            {
                fileServiceClient.Navigating += OnNavigatingRelay;
                fileServiceClient.Navigated += OnNavigatedRelay;

                try
                {
                    var result = await fileServiceClient.CommitAsync(resourceId, sessionId, filename, fileHash, fileSize, utFileParts);

                    switch (result.StatusCode)
                    {
                        case HttpStatusCode.OK:
                            OnFileUploadCompleted(new FileUploadCompletedEventArgs(
                                resourceId, sessionId, filename, result.Session.FileHash,
                                result.Session.SessionStartedDateTimeUtc, result.Session.SessionFinishedDateTimeUtc,
                                result.Session.SessionStartedBy));
                            break;

                        default:
                            OnUploadError(new UploadErrorEventArgs(result.FullUri, result.StatusCode.ToString(), result.Error, resourceId));
                            throw new Exception("Error" + result.StatusCode.ToString());
                    }
                }
                finally
                {
                    fileServiceClient.Navigating -= OnNavigatingRelay;
                    fileServiceClient.Navigated -= OnNavigatedRelay;
                }
            }
        }

        public void Dispose()
        {
        }

        private void OnNavigatedRelay(object sender, NavigatedEventArgs e)
        {
            OnNavigated(e);
        }

        private void OnNavigatingRelay(object sender, NavigatingEventArgs e)
        {
            OnNavigating(e);
        }

        private void OnNavigated(NavigatedEventArgs e)
        {
            Navigated?.Invoke(this, e);
        }

        private void OnNavigating(NavigatingEventArgs e)
        {
            Navigating?.Invoke(this, e);
        }

        private void OnPartUploaded(PartUploadedEventArgs e)
        {
            PartUploaded?.Invoke(this, e);
        }

        private void OnFileUploadCompleted(FileUploadCompletedEventArgs e)
        {
            FileUploadCompleted?.Invoke(this, e);
        }

        private void OnFileUploadStarted(FileUploadStartedEventArgs e)
        {
            FileUploadStarted?.Invoke(this, e);
        }

        private void OnUploadError(UploadErrorEventArgs e)
        {
            UploadError?.Invoke(this, e);
        }

        private void OnSessionCreated(SessionCreatedEventArgs e)
        {
            SessionCreated?.Invoke(this, e);
        }

        private void OnFileChecked(FileCheckedEventArgs e)
        {
            FileChecked?.Invoke(this, e);
        }

        private IFileServiceClient CreateFileServiceClient(string accessToken, string mdsBaseUrl)
        {
            return fileServiceClientFactory.CreateFileServiceClient(accessToken, mdsBaseUrl);
        }
    }
}



