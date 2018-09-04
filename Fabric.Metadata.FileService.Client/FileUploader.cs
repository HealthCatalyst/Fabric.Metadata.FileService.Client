﻿namespace Fabric.Metadata.FileService.Client
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Threading.Tasks;
    using System.Net;
    using System.Threading;
    using Events;
    using Exceptions;
    using FileServiceResults;
    using Interfaces;
    using Structures;

    public class FileUploader : IFileUploader
    {
        public event NavigatingEventHandler Navigating;
        public event NavigatedEventHandler Navigated;
        public event FileUploadStartedEventHandler FileUploadStarted;
        public event CalculatingHashEventHandler CalculatingHash;
        public event FileCheckedEventHandler FileChecked;
        public event SessionCreatedEventHandler SessionCreated;
        public event PartUploadedEventHandler PartUploaded;
        public event FileUploadCompletedEventHandler FileUploadCompleted;
        public event CommittingEventHandler Committing;

        public event UploadErrorEventHandler UploadError;
        public event TransientErrorEventHandler TransientError;
        public event AccessTokenRequestedEventHandler AccessTokenRequested;
        public event NewAccessTokenRequestedEventHandler NewAccessTokenRequested;

        private readonly IFileServiceClientFactory fileServiceClientFactory;
        private readonly IAccessTokenRepository accessTokenRepository;
        private readonly Uri mdsBaseUrl;
        private readonly Stopwatch watch;

        private const int SecondsToSleepBetweenCallingCheckCommit = 5;
        private const int NumberOfTimesToCallCheckCommit = 50;

        public FileUploader(
            IAccessTokenRepository accessTokenRepository,
            Uri mdsBaseUrl)
            : this(new FileServiceClientFactory(), accessTokenRepository, mdsBaseUrl)
        {
        }

        public FileUploader(IFileServiceClientFactory fileServiceClientFactory,
            IAccessTokenRepository accessTokenRepository,
            Uri mdsBaseUrl)
        {
            this.fileServiceClientFactory = fileServiceClientFactory;
            // ReSharper disable once JoinNullCheckWithUsage
            if (accessTokenRepository == null)
            {
                throw new ArgumentNullException(nameof(accessTokenRepository));
            }

            this.accessTokenRepository = accessTokenRepository;

            if (!mdsBaseUrl.ToString().EndsWith(@"/"))
            {
                mdsBaseUrl = new Uri($@"{mdsBaseUrl}/");
            }

            if (string.IsNullOrWhiteSpace(mdsBaseUrl.ToString())) throw new ArgumentNullException(nameof(mdsBaseUrl));

            this.mdsBaseUrl = mdsBaseUrl;
            this.watch = new Stopwatch();
        }

        public async Task UploadFileAsync(int resourceId, string filePath, CancellationToken ctsToken)
        {
            if (string.IsNullOrWhiteSpace(filePath)) throw new ArgumentNullException(nameof(filePath));
            if (resourceId <= 0) throw new ArgumentOutOfRangeException(nameof(resourceId));

            var fileSplitter = new FileSplitter();
            var fileName = Path.GetFileName(filePath);
            var fullFileSize = new FileInfo(filePath).Length;

            OnCalculatingHash(new CalculatingHashEventArgs(resourceId, filePath, fullFileSize));
            var hashForFile = new MD5FileHasher().CalculateHashForFile(filePath);

            // if there is no access token just error out now
            string accessToken = await this.accessTokenRepository.GetAccessTokenAsync();
            if (string.IsNullOrWhiteSpace(accessToken))
            {
                throw new InvalidAccessTokenException(accessToken);
            }

            using (var fileServiceClient = CreateFileServiceClient())
            {
                fileServiceClient.Navigating += RelayNavigatingEvent;
                fileServiceClient.Navigated += RelayNavigatedEvent;
                fileServiceClient.TransientError += RelayTransientErrorEvent;
                fileServiceClient.AccessTokenRequested += RelayAccessTokenRequestedEvent;
                fileServiceClient.NewAccessTokenRequested += RelayNewAccessTokenRequestedEvent;

                try
                {
                    // first check if the server already has the file
                    bool fileAlreadyExists = await CheckIfFileExistsOnServerAsync(fileServiceClient, resourceId, filePath, hashForFile);

                    ctsToken.ThrowIfCancellationRequested();

                    if (fileAlreadyExists) return;

                    // create new upload session
                    var uploadSession = await CreateNewUploadSessionAsync(fileServiceClient, resourceId);
                    ctsToken.ThrowIfCancellationRequested();

                    var countOfFileParts =
                        fileSplitter.GetCountOfFileParts(uploadSession.FileUploadChunkSizeInBytes, fullFileSize);

                    OnFileUploadStarted(new FileUploadStartedEventArgs(resourceId, uploadSession.SessionId, fileName,
                        countOfFileParts));

                    this.watch.Restart();

                    var fileParts = await fileSplitter.SplitFile(filePath, fileName,
                        uploadSession.FileUploadChunkSizeInBytes,
                        uploadSession.FileUploadMaxFileSizeInMegabytes,
                        async (stream, part) =>
                        {
                            ctsToken.ThrowIfCancellationRequested();
                            await this.UploadFilePartStreamAsync(fileServiceClient, stream, part, resourceId, uploadSession.SessionId, fileName, fullFileSize, countOfFileParts);
                        });

                    OnCommitting(new CommittingEventArgs(resourceId, uploadSession.SessionId, fileName, hashForFile, fullFileSize, fileParts));

                    var commitResult = await CommitAsync(fileServiceClient, resourceId, uploadSession.SessionId, fileName, hashForFile, fullFileSize, fileParts);

                    if (commitResult.StatusCode == HttpStatusCode.Accepted)
                    {
                        for (int i = 0; i < NumberOfTimesToCallCheckCommit; i++)
                        {
                            commitResult = await CheckCommitAsync(fileServiceClient, resourceId,
                                uploadSession.SessionId, fileName);
                            if (commitResult.StatusCode != HttpStatusCode.Accepted) break;
                            Thread.Sleep(SecondsToSleepBetweenCallingCheckCommit);
                        }
                    }

                    if (commitResult.StatusCode == HttpStatusCode.Accepted)
                    {
                        throw new InvalidOperationException("Server was not able to commit the file.  Please try again.");
                    }
                }
                finally
                {
                    fileServiceClient.Navigating -= RelayNavigatingEvent;
                    fileServiceClient.Navigated -= RelayNavigatedEvent;
                    fileServiceClient.TransientError -= RelayTransientErrorEvent;
                    fileServiceClient.AccessTokenRequested -= RelayAccessTokenRequestedEvent;
                    fileServiceClient.NewAccessTokenRequested -= RelayNewAccessTokenRequestedEvent;
                }
            }
        }

        public async Task DownloadFileAsync(int resourceId, string utTempFolder, CancellationToken ctsToken)
        {
            if (resourceId <= 0) throw new ArgumentOutOfRangeException(nameof(resourceId));

            using (var fileServiceClient = CreateFileServiceClient())
            {
                fileServiceClient.Navigating += RelayNavigatingEvent;
                fileServiceClient.Navigated += RelayNavigatedEvent;
                fileServiceClient.TransientError += RelayTransientErrorEvent;
                fileServiceClient.AccessTokenRequested += RelayAccessTokenRequestedEvent;
                fileServiceClient.NewAccessTokenRequested += RelayNewAccessTokenRequestedEvent;

                try
                {
                    var result = await fileServiceClient.DownloadFileAsync(resourceId, utTempFolder);

                    if (result.StatusCode == HttpStatusCode.OK)
                    {

                    }
                    else
                    {
                        OnUploadError(new UploadErrorEventArgs(resourceId, result.FullUri, result.StatusCode.ToString(), result.Error));
                    }
                }
                finally
                {
                    fileServiceClient.Navigating -= RelayNavigatingEvent;
                    fileServiceClient.Navigated -= RelayNavigatedEvent;
                    fileServiceClient.TransientError -= RelayTransientErrorEvent;
                    fileServiceClient.AccessTokenRequested -= RelayAccessTokenRequestedEvent;
                    fileServiceClient.NewAccessTokenRequested -= RelayNewAccessTokenRequestedEvent;
                }
            }
        }

        private async Task<bool> CheckIfFileExistsOnServerAsync(IFileServiceClient fileServiceClient, int resourceId,
            string filePath, string hashForFile)
        {
            if (fileServiceClient == null)
            {
                throw new ArgumentNullException(nameof(fileServiceClient));
            }

            if (resourceId <= 0) throw new ArgumentOutOfRangeException(nameof(resourceId));

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
                    OnUploadError(new UploadErrorEventArgs(resourceId, result.FullUri, result.StatusCode.ToString(),
                        result.Error));
                    throw new FileUploaderException(result.FullUri, result.StatusCode.ToString(), result.Error);
            }

            return false;
        }

        private async Task<UploadSession> CreateNewUploadSessionAsync(IFileServiceClient fileServiceClient,
            int resourceId)
        {
            if (fileServiceClient == null)
            {
                throw new ArgumentNullException(nameof(fileServiceClient));
            }

            if (resourceId <= 0) throw new ArgumentOutOfRangeException(nameof(resourceId));

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
                    OnUploadError(new UploadErrorEventArgs(resourceId, result.FullUri, result.StatusCode.ToString(),
                        result.Error));
                    throw new FileUploaderException(result.FullUri, result.StatusCode.ToString(), result.Error);
            }
        }

        // ReSharper disable once UnusedMember.Local
        private async Task<UploadSession> DeleteUploadSessionAsync(IFileServiceClient fileServiceClient, int resourceId)
        {
            if (fileServiceClient == null)
            {
                throw new ArgumentNullException(nameof(fileServiceClient));
            }

            if (resourceId <= 0) throw new ArgumentOutOfRangeException(nameof(resourceId));

            var result = await fileServiceClient.DeleteUploadSessionAsync(resourceId);

            switch (result.StatusCode)
            {
                case HttpStatusCode.OK:
                    return result.Session;

                default:
                    OnUploadError(new UploadErrorEventArgs(resourceId, result.FullUri, result.StatusCode.ToString(),
                        result.Error));
                    throw new FileUploaderException(result.FullUri, result.StatusCode.ToString(), result.Error);
            }
        }

        private async Task UploadFilePartStreamAsync(
            IFileServiceClient fileServiceClient, 
            Stream stream,
            FilePart filePart,
            int resourceId,
            Guid sessionId,
            string fileName,
            long fullFileSize,
            int filePartsCount)
        {
            if (fileServiceClient == null)
            {
                throw new ArgumentNullException(nameof(fileServiceClient));
            }

            if (filePart == null) throw new ArgumentNullException(nameof(filePart));
            if (fileName == null) throw new ArgumentNullException(nameof(fileName));
            if (resourceId <= 0) throw new ArgumentOutOfRangeException(nameof(resourceId));
            if (fullFileSize <= 0) throw new ArgumentOutOfRangeException(nameof(fullFileSize));
            if (filePartsCount <= 0) throw new ArgumentOutOfRangeException(nameof(filePartsCount));
            if (sessionId == default(Guid)) throw new ArgumentOutOfRangeException(nameof(sessionId));


            var result = await fileServiceClient.UploadStreamAsync(resourceId, sessionId, stream, filePart, fileName, fullFileSize, filePartsCount);

            switch (result.StatusCode)
            {
                case HttpStatusCode.OK:
                    TimeSpan timeElapsed = this.watch.Elapsed;
                    var ticksPerPart = timeElapsed.Ticks / result.PartsUploaded;
                    var estimatedTotalTicksLeft = ticksPerPart * (filePartsCount - result.PartsUploaded);

                    var estimatedTimeRemaining = new TimeSpan(estimatedTotalTicksLeft);

                    OnPartUploaded(
                        new PartUploadedEventArgs(resourceId, sessionId, fileName, filePart, result.StatusCode.ToString(), filePartsCount, result.PartsUploaded, estimatedTimeRemaining));

                    break;

                default:
                    OnUploadError(new UploadErrorEventArgs(resourceId, result.FullUri, result.StatusCode.ToString(), result.Error));
                    throw new FileUploaderException(result.FullUri, result.StatusCode.ToString(), result.Error);
            }

        }

        private async Task<CommitResult> CommitAsync(
            IFileServiceClient fileServiceClient, 
            int resourceId, 
            Guid sessionId,
            string filename, 
            string fileHash, 
            long fileSize, 
            IList<FilePart> utFileParts)
        {
            if (filename == null) throw new ArgumentNullException(nameof(filename));
            if (fileHash == null) throw new ArgumentNullException(nameof(fileHash));
            if (utFileParts == null) throw new ArgumentNullException(nameof(utFileParts));
            if (fileServiceClient == null)
            {
                throw new ArgumentNullException(nameof(fileServiceClient));
            }

            if (resourceId <= 0) throw new ArgumentOutOfRangeException(nameof(resourceId));
            if (fileSize <= 0) throw new ArgumentOutOfRangeException(nameof(fileSize));
            if (sessionId == default(Guid)) throw new ArgumentOutOfRangeException(nameof(sessionId));


            var result = await fileServiceClient.CommitAsync(resourceId, sessionId, filename, fileHash, fileSize, utFileParts);

            switch (result.StatusCode)
            {
                case HttpStatusCode.OK:
                    OnFileUploadCompleted(new FileUploadCompletedEventArgs(
                        resourceId,
                        sessionId,
                        filename,
                        result.Session.FileHash,
                        result.Session.SessionStartedDateTimeUtc,
                        result.Session.SessionFinishedDateTimeUtc,
                        result.Session.SessionStartedBy));
                    return result;

                case HttpStatusCode.Accepted:
                {
                    return result;
                }

                default:
                    OnUploadError(new UploadErrorEventArgs(resourceId, result.FullUri, result.StatusCode.ToString(), result.Error));
                    throw new FileUploaderException(result.FullUri, result.StatusCode.ToString(), result.Error);
            }
        }

        private async Task<CommitResult> CheckCommitAsync(
            IFileServiceClient fileServiceClient,
            int resourceId,
            Guid sessionId, 
            string fileName)
        {
            if (fileServiceClient == null)
            {
                throw new ArgumentNullException(nameof(fileServiceClient));
            }

            if (resourceId <= 0) throw new ArgumentOutOfRangeException(nameof(resourceId));
            if (sessionId == default(Guid)) throw new ArgumentOutOfRangeException(nameof(sessionId));

            var result = await fileServiceClient.CheckCommitAsync(resourceId, sessionId);

            switch (result.StatusCode)
            {
                case HttpStatusCode.OK:
                    OnFileUploadCompleted(new FileUploadCompletedEventArgs(
                        resourceId,
                        sessionId,
                        fileName,
                        result.Session.FileHash,
                        result.Session.SessionStartedDateTimeUtc,
                        result.Session.SessionFinishedDateTimeUtc,
                        result.Session.SessionStartedBy));

                    return result;

                case HttpStatusCode.Accepted:
                {
                    return result;
                }

                default:
                    OnUploadError(new UploadErrorEventArgs(resourceId, result.FullUri, result.StatusCode.ToString(), result.Error));
                    throw new FileUploaderException(result.FullUri, result.StatusCode.ToString(), result.Error);
            }
        }

        public void Dispose()
        {
        }

        private void RelayNavigatedEvent(object sender, NavigatedEventArgs e)
        {
            OnNavigated(e);
        }

        private void RelayNavigatingEvent(object sender, NavigatingEventArgs e)
        {
            OnNavigating(e);
        }

        private void RelayTransientErrorEvent(object sender, TransientErrorEventArgs e)
        {
            OnTransientError(e);
        }

        private void RelayAccessTokenRequestedEvent(object sender, AccessTokenRequestedEventArgs e)
        {
            OnAccessTokenRequested(e);
        }


        private void RelayNewAccessTokenRequestedEvent(object sender, NewAccessTokenRequestedEventArgs e)
        {
            OnNewAccessTokenRequested(e);
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

        private void OnTransientError(TransientErrorEventArgs e)
        {
            TransientError?.Invoke(this, e);
        }

        private void OnAccessTokenRequested(AccessTokenRequestedEventArgs e)
        {
            AccessTokenRequested?.Invoke(this, e);
        }
        private void OnNewAccessTokenRequested(NewAccessTokenRequestedEventArgs e)
        {
            NewAccessTokenRequested?.Invoke(this, e);
        }

        private void OnCalculatingHash(CalculatingHashEventArgs e)
        {
            CalculatingHash?.Invoke(this, e);
        }

        private void OnCommitting(CommittingEventArgs e)
        {
            Committing?.Invoke(this,e);
        }

        private IFileServiceClient CreateFileServiceClient()
        {
            return fileServiceClientFactory.CreateFileServiceClient(this.accessTokenRepository, this.mdsBaseUrl);
        }
    }
}



