using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Fabric.Metadata.FileService.Client;

namespace fabric.metadata.fileserver.client.console
{
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
            var mdsv2Url = Console.ReadLine();
            if (string.IsNullOrWhiteSpace(mdsv2Url)) mdsv2Url = Properties.Settings.Default.MdsV2Url;

            Properties.Settings.Default.MdsV2Url = mdsv2Url;

            var fileUploader = new FileUploader();
            fileUploader.Navigating += FileUploader_Navigating;
            fileUploader.Navigated += FileUploader_Navigated;
            fileUploader.PartUploaded += FileUploader_PartUploaded;
            fileUploader.FileUploadStarted += FileUploader_FileUploadStarted;
            fileUploader.FileUploadCompleted += FileUploader_FileUploadCompleted;
            fileUploader.UploadError += FileUploader_UploadError;
            fileUploader.SessionCreated += FileUploader_SessionCreated;
            fileUploader.FileChecked += FileUploader_FileChecked;

            Properties.Settings.Default.Save();

            var utTempFolder = Path.GetTempPath();
            await fileUploader.UploadFileAsync(filePath, accessToken, resourceId, utTempFolder, mdsv2Url);
        }

        private void FileUploader_FileChecked(object sender, Fabric.Metadata.FileService.Client.Events.FileCheckedEventArgs e)
        {
            var fileFound = e.FileFound ? "File found" : "File not found";

            if (e.HashMatches)
            {
                Console.WriteLine($"File matched: filename(server):[{e.FileNameOnServer}] lastmodified(server):{e.LastModifiedOnServer} Hash(local):[{e.HashForLocalFile}], Hash(server):[{e.HashOnServer}]");
            }
            else
            {
                Console.WriteLine($"File NOT matched: {fileFound} filename(server): {e.FileNameOnServer} lastmodified(server):{e.LastModifiedOnServer} Hash(local):[{e.HashForLocalFile}], Hash(server):[{e.HashOnServer}]");
            }

        }

        private void FileUploader_SessionCreated(object sender, Fabric.Metadata.FileService.Client.Events.SessionCreatedEventArgs e)
        {
            Console.WriteLine($"Session created: {e.SessionId}, Chunk size (bytes): {e.ChunkSizeInBytes}, Max File Size (MB): {e.MaxFileSizeInMegabytes}");
        }

        private void FileUploader_UploadError(object sender, Fabric.Metadata.FileService.Client.Events.UploadErrorEventArgs e)
        {
            Console.WriteLine("File Upload Error: " + e.Response);
        }

        private void FileUploader_FileUploadCompleted(object sender, Fabric.Metadata.FileService.Client.Events.FileUploadCompletedEventArgs e)
        {
            Console.WriteLine($"File Upload Completed: {e.FileName}");
        }
        private void FileUploader_FileUploadStarted(object sender, Fabric.Metadata.FileService.Client.Events.FileUploadStartedEventArgs e)
        {
            Console.WriteLine($"File Upload Started: {e.FileName} Parts={e.FilePartsCount}");
        }

        private void FileUploader_PartUploaded(object sender, Fabric.Metadata.FileService.Client.Events.PartUploadedEventArgs e)
        {
            Console.WriteLine($"Part Uploaded: {e.FileName} ({e.NumPartsUploaded}/{e.TotalFileParts}) {e.StatusCode}");
        }

        private void FileUploader_Navigated(object sender, Fabric.Metadata.FileService.Client.Events.NavigatedEventArgs e)
        {
            Console.WriteLine($"{e.Method} {e.FullUri} {e.StatusCode}");
        }

        private void FileUploader_Navigating(object sender, Fabric.Metadata.FileService.Client.Events.NavigatingEventArgs e)
        {
            Console.WriteLine($"{e.Method} {e.Uri}");
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
