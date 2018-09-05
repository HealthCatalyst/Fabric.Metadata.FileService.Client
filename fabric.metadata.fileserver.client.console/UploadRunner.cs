namespace fabric.metadata.fileserver.client.console
{
    using System;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;
    using Fabric.Metadata.FileService.Client;

    public class UploadRunner
    {
        public async Task RunAsync()
        {
            string accessToken = string.Empty;

            while (string.IsNullOrWhiteSpace(accessToken))
            {
                Console.WriteLine($"Paste in fabric identity access token [ENTER for {Properties.Settings.Default.AccessToken}]");
                accessToken = NewReadLine();
                if (string.IsNullOrWhiteSpace(accessToken))
                {
                    accessToken = Properties.Settings.Default.AccessToken;
                }
            }

            Properties.Settings.Default.AccessToken = accessToken;

            string filePath = string.Empty;

            while (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
            {
                Console.WriteLine($"Enter Full Path to file [ENTER for {Properties.Settings.Default.FilePath}]");
                filePath = Console.ReadLine();
                if (string.IsNullOrWhiteSpace(filePath))
                {
                    filePath = Properties.Settings.Default.FilePath;
                }
            }

            Properties.Settings.Default.FilePath = filePath;


            Console.WriteLine($"Enter resource id to use (ENTER for {Properties.Settings.Default.ResourceId})");
            var resourceIdText = Console.ReadLine();
            int resourceId = string.IsNullOrEmpty(resourceIdText) ? Properties.Settings.Default.ResourceId : Convert.ToInt32(resourceIdText);

            Properties.Settings.Default.ResourceId = resourceId;

            Console.WriteLine($"Enter url to MDS v2 [ENTER for {Properties.Settings.Default.MdsV2Url}]");
            var uriString = Console.ReadLine();
            var mdsV2Url = string.IsNullOrWhiteSpace(uriString) ? new Uri(Properties.Settings.Default.MdsV2Url) : new Uri(uriString);

            Properties.Settings.Default.MdsV2Url = mdsV2Url.ToString();

            var fileUploader = new FileUploader(new FileServiceAccessTokenRepository(accessToken), mdsV2Url);
            fileUploader.Navigating += FileUploader_Navigating;
            fileUploader.Navigated += FileUploader_Navigated;
            fileUploader.PartUploaded += FileUploader_PartUploaded;
            fileUploader.FileUploadStarted += FileUploader_FileUploadStarted;
            fileUploader.FileUploadCompleted += FileUploader_FileUploadCompleted;
            fileUploader.UploadError += FileUploader_UploadError;
            fileUploader.SessionCreated += FileUploader_SessionCreated;
            fileUploader.FileChecked += FileUploader_FileChecked;
            fileUploader.TransientError += FileUploader_TransientError;
            fileUploader.AccessTokenRequested += FileUploader_AccessTokenRequested;
            fileUploader.NewAccessTokenRequested += FileUploader_NewAccessTokenRequested;
            fileUploader.CalculatingHash += FileUploader_CalculatingHash;
            fileUploader.Committing += FileUploader_Committing;
            fileUploader.CheckingCommit += FileUploader_CheckingCommit;

            Properties.Settings.Default.Save();

            using (CancellationTokenSource cts = new CancellationTokenSource())
            {
                // var utTempFolder = Path.GetTempPath();
                try
                {
                    await fileUploader.UploadFileAsync(resourceId, filePath, cts.Token);
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                    throw;
                }
            }
        }

        private void FileUploader_CheckingCommit(object sender, Fabric.Metadata.FileService.Client.Events.CheckingCommitEventArgs e)
        {
            Console.WriteLine($"[{DateTime.Now:T}] Checking Commit: Times called: {e.TimesCalled}");
        }

        private void FileUploader_Committing(object sender, Fabric.Metadata.FileService.Client.Events.CommittingEventArgs e)
        {
            Console.WriteLine($"[{DateTime.Now:T}] Committing: {e.FileName}");
        }

        private void FileUploader_CalculatingHash(object sender, Fabric.Metadata.FileService.Client.Events.CalculatingHashEventArgs e)
        {
            Console.WriteLine($"[{DateTime.Now:T}] Calculating hash: file size: {e.FileSize}");
        }

        private void FileUploader_FileChecked(object sender, Fabric.Metadata.FileService.Client.Events.FileCheckedEventArgs e)
        {
            var fileFound = e.WasFileFound ? "File found" : "File not found";

            if (e.DidHashMatch)
            {
                Console.WriteLine($"[{DateTime.Now:T}] File matched: filename(server):[{e.FileNameOnServer}] lastmodified(server):{e.LastUploadedToServer} Hash(local):[{e.HashForLocalFile}], Hash(server):[{e.HashOnServer}]");
            }
            else
            {
                Console.WriteLine($"[{DateTime.Now:T}] File NOT matched: {fileFound} filename(server): {e.FileNameOnServer} lastmodified(server):{e.LastUploadedToServer} Hash(local):[{e.HashForLocalFile}], Hash(server):[{e.HashOnServer}]");
            }

        }

        private void FileUploader_SessionCreated(object sender, Fabric.Metadata.FileService.Client.Events.SessionCreatedEventArgs e)
        {
            Console.WriteLine($"[{DateTime.Now:T}] Session created: {e.SessionId}, Chunk size (bytes): {e.ChunkSizeInBytes}, Max File Size (MB): {e.MaxFileSizeInMegabytes}");
        }

        private void FileUploader_UploadError(object sender, Fabric.Metadata.FileService.Client.Events.UploadErrorEventArgs e)
        {
            Console.WriteLine("[{DateTime.Now:T}] File Upload Error: " + e.Response);
        }

        private void FileUploader_FileUploadCompleted(object sender, Fabric.Metadata.FileService.Client.Events.FileUploadCompletedEventArgs e)
        {
            Console.WriteLine($"[{DateTime.Now:T}] File Upload Completed: {e.FileName}, File Hash: {e.FileHash}, Session Finished: {e.SessionFinishedDateTimeUtc}");
        }
        private void FileUploader_FileUploadStarted(object sender, Fabric.Metadata.FileService.Client.Events.FileUploadStartedEventArgs e)
        {
            Console.WriteLine($"[{DateTime.Now:T}] File Upload Started: {e.FileName} Parts={e.TotalFileParts}");
        }

        private void FileUploader_PartUploaded(object sender, Fabric.Metadata.FileService.Client.Events.PartUploadedEventArgs e)
        {
            var estimatedTimeRemaining = e.EstimatedTimeRemaining.ToString(@"hh\:mm\:ss\.f");
            Console.WriteLine($"[{DateTime.Now:T}] Part Uploaded: {e.FileName} ({e.NumPartsUploaded}/{e.TotalFileParts}) {e.StatusCode}.  Est: {estimatedTimeRemaining}");
        }

        private void FileUploader_Navigated(object sender, Fabric.Metadata.FileService.Client.Events.NavigatedEventArgs e)
        {
            Console.WriteLine($"[{DateTime.Now:T}] {e.Method} {e.FullUri} {e.StatusCode}");
        }

        private void FileUploader_Navigating(object sender, Fabric.Metadata.FileService.Client.Events.NavigatingEventArgs e)
        {
            Console.WriteLine($"[{DateTime.Now:T}] {e.Method} {e.FullUri}");
        }

        private void FileUploader_TransientError(object sender, Fabric.Metadata.FileService.Client.Events.TransientErrorEventArgs e)
        {
            Console.WriteLine($"[{DateTime.Now:T}] Transient Error {e.Method} {e.FullUri} {e.StatusCode} {e.Response}.  Retry count {e.RetryCount}/{e.MaxRetryCount}.");
        }

        private void FileUploader_NewAccessTokenRequested(object sender, Fabric.Metadata.FileService.Client.Events.NewAccessTokenRequestedEventArgs e)
        {
            Console.WriteLine($"[{DateTime.Now:T}] New Access token requested for resource: {e.ResourceId}");
        }

        private void FileUploader_AccessTokenRequested(object sender, Fabric.Metadata.FileService.Client.Events.AccessTokenRequestedEventArgs e)
        {
            // Console.WriteLine($"An Access token requested for resource: {e.ResourceId}");
        }

        private static string NewReadLine()
        {
            Console.SetIn(new StreamReader(Console.OpenStandardInput(),
                Console.InputEncoding,
                false,
                bufferSize: 4096));
            string line = Console.ReadLine();

            return line;
        }
    }
}
