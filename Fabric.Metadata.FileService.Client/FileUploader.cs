using System.Linq;
using System.Net;
using System.Runtime.InteropServices.ComTypes;
using Fabric.Metadata.FileService.Client.Events;

namespace Fabric.Metadata.FileService.Client
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Net.Http;
    using System.Net.Http.Headers;
    using System.Text;
    using System.Threading.Tasks;
    using Newtonsoft.Json;

    public delegate void NavigatingEventHandler(object sender, NavigatingEventArgs e);
    public delegate void NavigatedEventHandler(object sender, NavigatedEventArgs e);
    public delegate void PartUploadedEventHandler(object sender, PartUploadedEventArgs e);
    public delegate void FileUploadStartedEventHandler(object sender, FileUploadStartedEventArgs e);
    public delegate void FileUploadCompletedEventHandler(object sender, FileUploadCompletedEventArgs e);
    public delegate void UploadErrorEventHandler(object sender, UploadErrorEventArgs e);
    public delegate void SessionCreatedEventHandler(object sender, SessionCreatedEventArgs e);
    public delegate void FileCheckedEventHandler(object sender, FileCheckedEventArgs e);

    public class FileUploader
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

        public async Task UploadFileAsync(string filePath, string accessToken, int resourceId, string utTempFolder, string mdsBaseUrl)
        {
            if (string.IsNullOrWhiteSpace(filePath)) throw new ArgumentNullException(nameof(filePath));
            if (string.IsNullOrWhiteSpace(accessToken)) throw new ArgumentNullException(nameof(accessToken));
            if (string.IsNullOrWhiteSpace(utTempFolder)) throw new ArgumentNullException(nameof(utTempFolder));
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
            bool fileAlreadyExists = await CheckIfFileExistsOnServer(mdsBaseUrl, accessToken, resourceId, fileName, filePath, hashForFile);

            if (fileAlreadyExists) return;

            // create new upload session
            var uploadSession = await CreateNewUploadSessionAsync(mdsBaseUrl, accessToken, resourceId);

            numPartsUploaded = 0;

            var fullFileSize = new FileInfo(filePath).Length;

            var countOfFileParts = fileSplitter.GetCountOfFileParts(uploadSession.FileUploadChunkSizeInBytes, fullFileSize);

            OnFileUploadStarted(new FileUploadStartedEventArgs(fileName, countOfFileParts));

            var fileParts = await fileSplitter.SplitFile(filePath, fileName, utTempFolder, uploadSession.FileUploadChunkSizeInBytes,
                uploadSession.FileUploadMaxFileSizeInMegabytes, 
                (stream, part) => UploadFilePartStreamAsync(stream, part, mdsBaseUrl, accessToken, resourceId, uploadSession.SessionId, fileName, fullFileSize, countOfFileParts) );



            // call CommitAsync
            await CommitAsync(mdsBaseUrl, accessToken, resourceId, uploadSession.SessionId, fileName, hashForFile,
                fullFileSize, fileParts);
        }

        private async Task UploadFilePartStreamAsync(Stream stream, FilePart part, string mdsBaseUrl, string accessToken, int resourceId, Guid uploadSessionId, string fileName, long fullFileSize, int totalFileParts)
        {
            await InternalUploadStreamAsync(stream, part, mdsBaseUrl, accessToken, resourceId, uploadSessionId, fileName, fullFileSize, totalFileParts);
        }

        public async Task DownloadFileAsync(string accessToken, int resourceId, string utTempFolder, string mdsBaseUrl)
        {
            if (string.IsNullOrWhiteSpace(accessToken)) throw new ArgumentNullException(nameof(accessToken));
            if (string.IsNullOrWhiteSpace(utTempFolder)) throw new ArgumentNullException(nameof(utTempFolder));
            if (string.IsNullOrWhiteSpace(mdsBaseUrl)) throw new ArgumentNullException(nameof(mdsBaseUrl));
            if (resourceId <= 0) throw new ArgumentOutOfRangeException(nameof(resourceId));

            if (!mdsBaseUrl.EndsWith(@"/"))
            {
                mdsBaseUrl += @"/";
            }

            await DownloadFile(mdsBaseUrl, accessToken, resourceId, utTempFolder);
        }

        private HttpClient CreateHttpClient(string accessToken)
        {
            if (accessToken == null) throw new ArgumentNullException(nameof(accessToken));

            var httpClient = new HttpClient();
            httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            return httpClient;
        }


        private async Task<bool> CheckIfFileExistsOnServer(string mdsBaseUrl, string accessToken, int resourceId,
            string fileName, string filePath, string hashForFile)
        {
            if (mdsBaseUrl == null) throw new ArgumentNullException(nameof(mdsBaseUrl));
            if (accessToken == null) throw new ArgumentNullException(nameof(accessToken));
            if (resourceId <= 0) throw new ArgumentOutOfRangeException(nameof(resourceId));

            using (var httpClient = CreateHttpClient(accessToken))
            {
                var baseUri = new Uri(mdsBaseUrl);
                var fullUri = new Uri(baseUri, $"Files({resourceId})");

                var method = Convert.ToString(HttpMethod.Head);
                OnNavigate(new NavigatingEventArgs(fullUri, method));

                var result = await httpClient.SendAsync(new HttpRequestMessage(HttpMethod.Head, fullUri));

                OnNavigated(new NavigatedEventArgs(method, fullUri, result.StatusCode.ToString()));

                if (result.StatusCode == HttpStatusCode.NoContent)
                {
                    var headersLastModified = result.Content.Headers.LastModified;
                    var headersContentMd5 = result.Content.Headers.ContentMD5;
                    var contentDispositionFileName = result.Content.Headers.ContentDisposition?.FileName;

                    if (headersContentMd5 != null)
                    {
                        var hashForFileOnServer = Encoding.UTF8.GetString(headersContentMd5);

                        OnFileChecked(new FileCheckedEventArgs(true, hashForFile, hashForFileOnServer, headersLastModified, contentDispositionFileName, hashForFile == hashForFileOnServer));

                        if (hashForFile == hashForFileOnServer)
                        {
                            return true;
                        }
                    }
                }
                else if (result.StatusCode == HttpStatusCode.NotFound)
                {
                    // this is acceptable response if the file does not exist on the server
                }
                else
                {
                    OnUploadError(new UploadErrorEventArgs(fullUri, result.StatusCode.ToString(), await result.Content.ReadAsStringAsync()));
                }
            }

            return false;
        }
        private async Task DownloadFile(string mdsBaseUrl, string accessToken, int resourceId, string utTempPath)
        {
            if (mdsBaseUrl == null) throw new ArgumentNullException(nameof(mdsBaseUrl));
            if (accessToken == null) throw new ArgumentNullException(nameof(accessToken));
            if (resourceId <= 0) throw new ArgumentOutOfRangeException(nameof(resourceId));

            using (var httpClient = CreateHttpClient(accessToken))
            {
                var baseUri = new Uri(mdsBaseUrl);
                var fullUri = new Uri(baseUri, $"Files({resourceId})");

                var method = Convert.ToString(HttpMethod.Get);
                OnNavigate(new NavigatingEventArgs(fullUri, method));

                var result = await httpClient.GetAsync(fullUri);

                OnNavigated(new NavigatedEventArgs(method, fullUri, result.StatusCode.ToString()));

                if (result.IsSuccessStatusCode)
                {
                    var headersLastModified = result.Content.Headers.LastModified;
                    var headersContentMd5 = result.Content.Headers.ContentMD5;
                    var contentDispositionFileName = result.Content.Headers.ContentDisposition?.FileName;

                    var contentStream = await result.Content.ReadAsStreamAsync();

                    var bufferSize = contentStream.Length;

                    if (contentDispositionFileName != null)
                    {
                        var fullPath = Path.Combine(utTempPath, contentDispositionFileName);
                        using (FileStream destinationStream =
                            new FileStream(fullPath, FileMode.Create, FileAccess.Write, FileShare.None, bufferSize: Convert.ToInt32(bufferSize), useAsync: true))
                        {
                            await contentStream.CopyToAsync(destinationStream);
                        }
                    }
                }
            }
        }

        private void OnFileChecked(FileCheckedEventArgs e)
        {
            FileChecked?.Invoke(this, e);
        }

        private async Task<UploadSession> CreateNewUploadSessionAsync(string mdsBaseUrl, string accessToken, int resourceId)
        {
            if (mdsBaseUrl == null) throw new ArgumentNullException(nameof(mdsBaseUrl));
            if (accessToken == null) throw new ArgumentNullException(nameof(accessToken));
            if (resourceId <= 0) throw new ArgumentOutOfRangeException(nameof(resourceId));

            using (var httpClient = CreateHttpClient(accessToken))
            {
                var form = new
                {

                };

                var baseUri = new Uri(mdsBaseUrl);
                var fullUri = new Uri(baseUri, $"Files({resourceId})/UploadSessions");

                var method = Convert.ToString(HttpMethod.Post);
                OnNavigate(new NavigatingEventArgs(fullUri, method));

                var result = await httpClient.PostAsync(
                    fullUri,
                    new StringContent(JsonConvert.SerializeObject(form),
                        Encoding.UTF8,
                        "application/json"));

                OnNavigated(new NavigatedEventArgs(method, fullUri, result.StatusCode.ToString()));

                if (result.IsSuccessStatusCode)
                {
                    var content = await result.Content.ReadAsStringAsync();

                    dynamic clientResponse = JsonConvert.DeserializeObject(content);
                    string sessionIdText = (string)clientResponse["SessionId"];
                    var sessionId = Guid.Parse(sessionIdText);
                    var fileUploadChunkSizeInBytes = Convert.ToInt64((string)clientResponse["FileUploadChunkSizeInBytes"]);
                    var fileUploadMaxFileSizeInMegabytes = Convert.ToInt64((string)clientResponse["FileUploadMaxFileSizeInMegabytes"]);

                    OnSessionCreated(new SessionCreatedEventArgs(sessionId, fileUploadChunkSizeInBytes, fileUploadMaxFileSizeInMegabytes));

                    return new UploadSession
                    {
                        SessionId = sessionId,
                        FileUploadChunkSizeInBytes = fileUploadChunkSizeInBytes,
                        FileUploadMaxFileSizeInMegabytes = fileUploadMaxFileSizeInMegabytes
                    };
                }
                else if (result.StatusCode == HttpStatusCode.BadRequest)
                {
                    var content = await result.Content.ReadAsStringAsync();
                    dynamic clientResponse = JsonConvert.DeserializeObject(content);
                    var errorCode = clientResponse["ErrorCode"] != null ? Convert.ToString(clientResponse["ErrorCode"]) : null;
                    if (errorCode != null)
                    {
                        if (errorCode == Enum.GetName(typeof(FileServiceErrorCode),
                                FileServiceErrorCode.SessionAlreadyExists))
                        {
                            // delete the session and try again
                            await DeleteUploadSessionAsync(mdsBaseUrl, accessToken, resourceId);

                            return await CreateNewUploadSessionAsync(mdsBaseUrl, accessToken, resourceId);
                        }
                    }
                }
                else
                {
                    var content = await result.Content.ReadAsStringAsync();
                    OnUploadError(new UploadErrorEventArgs(fullUri, result.StatusCode.ToString(), content));
                }

                throw new Exception("Error" + result.StatusCode.ToString());
            }
        }
        private async Task<UploadSession> DeleteUploadSessionAsync(string mdsBaseUrl, string accessToken, int resourceId)
        {
            if (mdsBaseUrl == null) throw new ArgumentNullException(nameof(mdsBaseUrl));
            if (accessToken == null) throw new ArgumentNullException(nameof(accessToken));
            if (resourceId <= 0) throw new ArgumentOutOfRangeException(nameof(resourceId));

            using (var httpClient = CreateHttpClient(accessToken))
            {
                var baseUri = new Uri(mdsBaseUrl);
                var fullUri = new Uri(baseUri, $"Files({resourceId})/UploadSessions");

                var method = Convert.ToString(HttpMethod.Delete);
                OnNavigate(new NavigatingEventArgs(fullUri, method));

                var result = await httpClient.DeleteAsync(fullUri);

                OnNavigated(new NavigatedEventArgs(method, fullUri, result.StatusCode.ToString()));

                if (result.IsSuccessStatusCode)
                {
                }
                else if (result.StatusCode == HttpStatusCode.BadRequest)
                {
                    var content = await result.Content.ReadAsStringAsync();
                    dynamic clientResponse = JsonConvert.DeserializeObject(content);
                    var errorCode = clientResponse["ErrorCode"] != null ? Convert.ToString(clientResponse["ErrorCode"]) : null;
                    if (errorCode != null)
                    {
                    }
                    OnUploadError(new UploadErrorEventArgs(fullUri, result.StatusCode.ToString(), content));
                }
                else
                {
                    var content = await result.Content.ReadAsStringAsync();
                    OnUploadError(new UploadErrorEventArgs(fullUri, result.StatusCode.ToString(), content));
                }

                throw new Exception("Error" + result.StatusCode.ToString());
            }
        }


        private async Task<bool> InternalUploadStreamAsync(Stream stream, FilePart filePart, string mdsBaseUrl,
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

            bool rslt = false;
            using (var httpClient = CreateHttpClient(accessToken))
            {
                using (var content = new MultipartFormDataContent())
                {
                    stream.Seek(0, SeekOrigin.Begin);

                    var fileContent = new StreamContent(stream);
                    fileContent.Headers.ContentDisposition = new
                        ContentDispositionHeaderValue("attachment")
                    {
                        FileName = fileName
                    };
                    fileContent.Headers.ContentRange =
                        new ContentRangeHeaderValue(filePart.Offset,
                            filePart.Offset + filePart.Size);
                    fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
                    fileContent.Headers.ContentMD5 = Encoding.UTF8.GetBytes(filePart.Hash);
                    content.Add(fileContent);

                    var baseUri = new Uri(mdsBaseUrl);
                    var fullUri = new Uri(baseUri, $"Files({resourceId})/UploadSessions({sessionId})");

                    var method = Convert.ToString(HttpMethod.Put);
                    OnNavigate(new NavigatingEventArgs(fullUri, method));

                    try
                    {
                        var result = httpClient.PutAsync(fullUri, content).Result;

                        OnNavigated(new NavigatedEventArgs(method, fullUri, result.StatusCode.ToString()));

                        if (result.IsSuccessStatusCode)
                        {
                            numPartsUploaded++;
                            OnPartUploaded(
                                new PartUploadedEventArgs(fileName, filePart, result.StatusCode.ToString(), filePartsCount, numPartsUploaded));
                        }
                        else
                        {
                            var errorText = await result.Content.ReadAsStringAsync();
                            throw new Exception($"Error [{result.StatusCode}] {errorText}");
                        }

                        rslt = true;
                    }
                    catch (Exception ex)
                    {
                        // log error  
                        rslt = false;
                        throw;
                    }
                }
            }
            return rslt;
        }

        private async Task CommitAsync(string mdsBaseUrl, string accessToken, int resourceId, Guid sessionId,
            string filename, string filehash, long fileSize, IList<FilePart> utFileParts)
        {
            if (mdsBaseUrl == null) throw new ArgumentNullException(nameof(mdsBaseUrl));
            if (accessToken == null) throw new ArgumentNullException(nameof(accessToken));
            if (filename == null) throw new ArgumentNullException(nameof(filename));
            if (filehash == null) throw new ArgumentNullException(nameof(filehash));
            if (utFileParts == null) throw new ArgumentNullException(nameof(utFileParts));
            if (resourceId <= 0) throw new ArgumentOutOfRangeException(nameof(resourceId));
            if (fileSize <= 0) throw new ArgumentOutOfRangeException(nameof(fileSize));
            if (sessionId == default(Guid)) throw new ArgumentOutOfRangeException(nameof(sessionId));

            using (var httpClient = CreateHttpClient(accessToken))
            {
                var form = new
                {
                    FileDetail = new
                    {
                        FileName = filename,
                        Hash = filehash,
                        Size = fileSize,
                        Parts = utFileParts.Select(p => new FilePart
                            {
                                Id = p.Id,
                                Hash = p.Hash,
                                Size = p.Size,
                                Offset = p.Offset
                            })
                            .ToList()
                    }
                };

                var baseUri = new Uri(mdsBaseUrl);
                var fullUri = new Uri(baseUri, $"Files({resourceId})/UploadSessions({sessionId})/MetadataService.Commit");

                var method = Convert.ToString(HttpMethod.Post);
                OnNavigate(new NavigatingEventArgs(fullUri, method));

                var result = await httpClient.PostAsync(
                    fullUri,
                    new StringContent(JsonConvert.SerializeObject(form), Encoding.UTF8, "application/json"));

                OnNavigated(new NavigatedEventArgs(method, fullUri, result.StatusCode.ToString()));

                if (result.IsSuccessStatusCode)
                {
                    OnFileUploadCompleted(new FileUploadCompletedEventArgs(filename));
                }
                else
                {
                    var errorText = await result.Content.ReadAsStringAsync();
                    throw new Exception($"Error [{result.StatusCode}] {errorText}");
                }

            }
        }

        private void OnNavigated(NavigatedEventArgs e)
        {
            Navigated?.Invoke(this, e);
        }

        private void OnPartUploaded(PartUploadedEventArgs e)
        {
            PartUploaded?.Invoke(this, e);
        }

        private void OnNavigate(NavigatingEventArgs e)
        {
            Navigating?.Invoke(this, e);
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


    }
}



