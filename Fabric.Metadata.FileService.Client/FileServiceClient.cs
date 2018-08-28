namespace Fabric.Metadata.FileService.Client
{
    using Events;
    using FileServiceResults;
    using Newtonsoft.Json;
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Net.Http;
    using System.Net.Http.Headers;
    using System.Text;
    using System.Threading.Tasks;

    /// <inheritdoc />
    /// <summary>
    /// This class manages the communication with the DOS File Service
    /// </summary>
    public class FileServiceClient : IDisposable
    {
        public event NavigatingEventHandler Navigating;
        public event NavigatedEventHandler Navigated;

        private const string DispositionType = "attachment";
        private const string ApplicationJsonMediaType = "application/json";
        private const string ApplicationOctetStreamMediaType = "application/octet-stream";

        private readonly HttpClient httpClient;
        private readonly string mdsBaseUrl;

        public FileServiceClient(string accessToken, string mdsBaseUrl)
        {
            if (string.IsNullOrWhiteSpace(accessToken))
            {
                throw new ArgumentNullException(nameof(accessToken));
            }

            if (string.IsNullOrWhiteSpace(mdsBaseUrl))
            {
                throw new ArgumentNullException(nameof(mdsBaseUrl));
            }

            if (!mdsBaseUrl.EndsWith(@"/"))
            {
                mdsBaseUrl += @"/";
            }

            this.httpClient = this.CreateHttpClient(accessToken);
            this.mdsBaseUrl = mdsBaseUrl;
        }

        /// <summary>
        /// This calls HEAD Files({resourceId})
        /// </summary>
        /// <param name="resourceId"></param>
        /// <returns></returns>
        public async Task<CheckFileResult> CheckFileAsync(int resourceId)
        {
            if (resourceId <= 0) throw new ArgumentOutOfRangeException(nameof(resourceId));

            var baseUri = new Uri(mdsBaseUrl);
            var fullUri = new Uri(baseUri, $"Files({resourceId})");

            var method = Convert.ToString(HttpMethod.Head);

            OnNavigating(new NavigatingEventArgs(resourceId, fullUri, method));

            var httpResponse = await httpClient.SendAsync(new HttpRequestMessage(HttpMethod.Head, fullUri));

            OnNavigated(new NavigatedEventArgs(resourceId, method, fullUri, httpResponse.StatusCode.ToString()));

            switch (httpResponse.StatusCode)
            {
                case HttpStatusCode.NoContent:
                    {
                        // HEAD returns NoContent on success

                        var headersLastModified = httpResponse.Content.Headers.LastModified;
                        var headersContentMd5 = httpResponse.Content.Headers.ContentMD5;
                        var contentDispositionFileName = httpResponse.Content.Headers.ContentDisposition?.FileName;

                        var result = new CheckFileResult
                        {
                            StatusCode = httpResponse.StatusCode,
                            LastModified = headersLastModified,
                            FileNameOnServer = contentDispositionFileName,
                            FullUri = fullUri
                        };

                        if (headersContentMd5 != null)
                        {
                            result.HashForFileOnServer = Encoding.UTF8.GetString(headersContentMd5);
                        }

                        return result;
                    }
                case HttpStatusCode.NotFound:
                    // this is acceptable response if the file does not exist on the server
                    return new CheckFileResult
                    {
                        StatusCode = httpResponse.StatusCode,
                        FullUri = fullUri
                    };
                default:
                    {
                        var content = await httpResponse.Content.ReadAsStringAsync();
                        return new CheckFileResult
                        {
                            StatusCode = httpResponse.StatusCode,
                            Error = content,
                            FullUri = fullUri
                        };
                    }
            }
        }

        /// <summary>
        /// This calls POST Files({resourceId})/UploadSessions
        /// </summary>
        /// <param name="resourceId"></param>
        /// <returns></returns>
        public async Task<CreateSessionResult> CreateNewUploadSessionAsync(int resourceId)
        {
            if (resourceId <= 0) throw new ArgumentOutOfRangeException(nameof(resourceId));

            var baseUri = new Uri(mdsBaseUrl);
            var fullUri = new Uri(baseUri, $"Files({resourceId})/UploadSessions");

            var method = Convert.ToString(HttpMethod.Post);

            OnNavigating(new NavigatingEventArgs(resourceId, fullUri, method));

            var form = new
            {

            };

            var httpResponse = await httpClient.PostAsync(
                fullUri,
                new StringContent(JsonConvert.SerializeObject(form),
                    Encoding.UTF8,
                    ApplicationJsonMediaType));

            OnNavigated(new NavigatedEventArgs(resourceId, method, fullUri, httpResponse.StatusCode.ToString()));

            switch (httpResponse.StatusCode)
            {
                case HttpStatusCode.OK:
                    {
                        var content = await httpResponse.Content.ReadAsStringAsync();

                        var clientResponse = JsonConvert.DeserializeObject<UploadSession>(content);

                        var result = new CreateSessionResult
                        {
                            StatusCode = httpResponse.StatusCode,
                            FullUri = fullUri,
                            Session = clientResponse
                        };

                        return result;
                    }
                case HttpStatusCode.BadRequest:
                    {
                        var content = await httpResponse.Content.ReadAsStringAsync();
                        dynamic clientResponse = JsonConvert.DeserializeObject(content);
                        var errorCode = clientResponse["ErrorCode"] != null ? Convert.ToString(clientResponse["ErrorCode"]) : null;

                        return new CreateSessionResult
                        {
                            StatusCode = httpResponse.StatusCode,
                            FullUri = fullUri,
                            ErrorCode = errorCode,
                            Error = content
                        };
                    }
                default:
                    {
                        var content = await httpResponse.Content.ReadAsStringAsync();
                        return new CreateSessionResult
                        {
                            StatusCode = httpResponse.StatusCode,
                            Error = content,
                            FullUri = fullUri
                        };
                    }
            }
        }

        /// <summary>
        /// This method calls PUT Files({resourceId})/UploadSessions
        /// </summary>
        /// <param name="resourceId"></param>
        /// <param name="sessionId"></param>
        /// <param name="stream"></param>
        /// <param name="filePart"></param>
        /// <param name="fileName"></param>
        /// <param name="fullFileSize"></param>
        /// <param name="filePartsCount"></param>
        /// <param name="numPartsUploaded"></param>
        /// <returns></returns>
        public async Task<UploadStreamResult> UploadStreamAsync(int resourceId,
            Guid sessionId,
            Stream stream,
            FilePart filePart,
            string fileName,
            long fullFileSize,
            int filePartsCount,
            int numPartsUploaded)
        {
            if (resourceId <= 0) throw new ArgumentOutOfRangeException(nameof(resourceId));
            if (numPartsUploaded < 0) throw new ArgumentOutOfRangeException(nameof(numPartsUploaded));
            if (filePartsCount < 0) throw new ArgumentOutOfRangeException(nameof(filePartsCount));

            var baseUri = new Uri(mdsBaseUrl);
            var fullUri = new Uri(baseUri, $"Files({resourceId})/UploadSessions");

            var method = Convert.ToString(HttpMethod.Put);

            using (var requestContent = new MultipartFormDataContent())
            {
                stream.Seek(0, SeekOrigin.Begin);

                var fileContent = new StreamContent(stream);

                fileContent.Headers.ContentDisposition = new
                    ContentDispositionHeaderValue(DispositionType)
                {
                    FileName = fileName
                };

                fileContent.Headers.ContentRange =
                    new ContentRangeHeaderValue(filePart.Offset, filePart.Offset + filePart.Size);

                fileContent.Headers.ContentType = new MediaTypeHeaderValue(ApplicationOctetStreamMediaType);
                fileContent.Headers.ContentMD5 = Encoding.UTF8.GetBytes(filePart.Hash);
                requestContent.Add(fileContent);

                OnNavigating(new NavigatingEventArgs(resourceId, fullUri, method));

                var httpResponse = httpClient.PutAsync(fullUri, requestContent).Result;

                OnNavigated(new NavigatedEventArgs(resourceId, method, fullUri, httpResponse.StatusCode.ToString()));

                switch (httpResponse.StatusCode)
                {
                    case HttpStatusCode.OK:
                        return new UploadStreamResult
                        {
                            // ReSharper disable once RedundantAssignment
                            PartsUploaded = numPartsUploaded++
                        };
                    default:
                    {
                        var content = await httpResponse.Content.ReadAsStringAsync();
                        return new UploadStreamResult
                        {
                            StatusCode = httpResponse.StatusCode,
                            Error = content,
                            FullUri = fullUri
                        };
                    }
                }
            }
        }

        /// <summary>
        /// This calls POST Files({resourceId})/UploadSessions({sessionId})/MetadataService.Commit
        /// </summary>
        /// <param name="resourceId"></param>
        /// <param name="sessionId"></param>
        /// <param name="filename"></param>
        /// <param name="fileHash"></param>
        /// <param name="fileSize"></param>
        /// <param name="utFileParts"></param>
        /// <returns></returns>
        public async Task<CommitResult> CommitAsync(int resourceId, Guid sessionId,
            string filename, string fileHash, long fileSize, IList<FilePart> utFileParts)
        {
            if (resourceId <= 0) throw new ArgumentOutOfRangeException(nameof(resourceId));

            var baseUri = new Uri(mdsBaseUrl);
            var fullUri = new Uri(baseUri, $"Files({resourceId})/UploadSessions({sessionId})/MetadataService.Commit");

            var method = Convert.ToString(HttpMethod.Post);

            OnNavigating(new NavigatingEventArgs(resourceId, fullUri, method));

            var form = new
            {
                FileDetail = new
                {
                    FileName = filename,
                    Hash = fileHash,
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

            var httpResponse = await httpClient.PostAsync(
                fullUri,
                new StringContent(JsonConvert.SerializeObject(form),
                    Encoding.UTF8,
                    ApplicationJsonMediaType));

            OnNavigated(new NavigatedEventArgs(resourceId, method, fullUri, httpResponse.StatusCode.ToString()));

            switch (httpResponse.StatusCode)
            {
                case HttpStatusCode.OK:
                {
                    var content = await httpResponse.Content.ReadAsStringAsync();

                    var clientResponse = JsonConvert.DeserializeObject<UploadSession>(content);

                    var result = new CommitResult
                    {
                        StatusCode = httpResponse.StatusCode,
                        FullUri = fullUri,
                        Session = clientResponse
                    };

                    return result;
                }
                case HttpStatusCode.BadRequest:
                {
                    var content = await httpResponse.Content.ReadAsStringAsync();
                    dynamic clientResponse = JsonConvert.DeserializeObject(content);
                    var errorCode = clientResponse["ErrorCode"] != null ? Convert.ToString(clientResponse["ErrorCode"]) : null;

                    return new CommitResult
                    {
                        StatusCode = httpResponse.StatusCode,
                        FullUri = fullUri,
                        ErrorCode = errorCode,
                        Error = content
                    };
                }
                default:
                {
                    var content = await httpResponse.Content.ReadAsStringAsync();
                    return new CommitResult
                    {
                        StatusCode = httpResponse.StatusCode,
                        Error = content,
                        FullUri = fullUri
                    };
                }
            }
        }

        /// <summary>
        /// This calls GET Files({resourceId})
        /// </summary>
        /// <param name="resourceId"></param>
        /// <param name="utTempPath"></param>
        /// <returns></returns>
        public async Task<CheckFileResult> DownloadFileAsync(int resourceId, string utTempPath)
        {
            if (resourceId <= 0) throw new ArgumentOutOfRangeException(nameof(resourceId));

            var baseUri = new Uri(mdsBaseUrl);
            var fullUri = new Uri(baseUri, $"Files({resourceId})");

            var method = Convert.ToString(HttpMethod.Get);

            OnNavigating(new NavigatingEventArgs(resourceId, fullUri, method));

            var httpResponse = await httpClient.GetAsync(fullUri);

            OnNavigated(new NavigatedEventArgs(resourceId, method, fullUri, httpResponse.StatusCode.ToString()));

            switch (httpResponse.StatusCode)
            {
                case HttpStatusCode.OK:
                {
                    var headersLastModified = httpResponse.Content.Headers.LastModified;
                    var headersContentMd5 = httpResponse.Content.Headers.ContentMD5;
                    var contentDispositionFileName = httpResponse.Content.Headers.ContentDisposition?.FileName;

                    var result = new CheckFileResult
                    {
                        StatusCode = httpResponse.StatusCode,
                        LastModified = headersLastModified,
                        FileNameOnServer = contentDispositionFileName,
                        FullUri = fullUri
                    };

                    if (headersContentMd5 != null)
                    {
                        result.HashForFileOnServer = Encoding.UTF8.GetString(headersContentMd5);
                    }

                    var contentStream = await httpResponse.Content.ReadAsStreamAsync();

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
                    return result;
                }
                case HttpStatusCode.NotFound:
                    // this is acceptable response if the file does not exist on the server
                    return new CheckFileResult
                    {
                        StatusCode = httpResponse.StatusCode,
                        FullUri = fullUri
                    };
                default:
                {
                    var content = await httpResponse.Content.ReadAsStringAsync();
                    return new CheckFileResult
                    {
                        StatusCode = httpResponse.StatusCode,
                        Error = content,
                        FullUri = fullUri
                    };
                }
            }
        }

        /// <summary>
        /// This calls DELETE Files({resourceId})/UploadSessions
        /// </summary>
        /// <param name="resourceId"></param>
        /// <returns></returns>
        public async Task<DeleteSessionResult> DeleteUploadSessionAsync(int resourceId)
        {
            if (resourceId <= 0) throw new ArgumentOutOfRangeException(nameof(resourceId));

            var baseUri = new Uri(mdsBaseUrl);
            var fullUri = new Uri(baseUri, $"Files({resourceId})/UploadSessions");

            var method = Convert.ToString(HttpMethod.Delete);

            OnNavigating(new NavigatingEventArgs(resourceId, fullUri, method));

            var httpResponse = await httpClient.DeleteAsync(fullUri);

            OnNavigated(new NavigatedEventArgs(resourceId, method, fullUri, httpResponse.StatusCode.ToString()));

            switch (httpResponse.StatusCode)
            {
                case HttpStatusCode.OK:
                {
                    var content = await httpResponse.Content.ReadAsStringAsync();

                    var uploadSession = JsonConvert.DeserializeObject<UploadSession>(content);

                    return new DeleteSessionResult
                    {
                        StatusCode = httpResponse.StatusCode,
                        FullUri = fullUri,
                        Session = uploadSession
                    };
                }
                default:
                {
                    var content = await httpResponse.Content.ReadAsStringAsync();
                    return new DeleteSessionResult
                    {
                        StatusCode = httpResponse.StatusCode,
                        Error = content,
                        FullUri = fullUri
                    };
                }
            }
        }

        public void Dispose()
        {
            this.httpClient.Dispose();
        }

        private HttpClient CreateHttpClient(string accessToken)
        {
            if (accessToken == null) throw new ArgumentNullException(nameof(accessToken));

            var createHttpClient = new HttpClient();
            createHttpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue(ApplicationJsonMediaType));
            createHttpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            return createHttpClient;
        }

        private void OnNavigating(NavigatingEventArgs e)
        {
            Navigating?.Invoke(this, e);
        }
        private void OnNavigated(NavigatedEventArgs e)
        {
            Navigated?.Invoke(this, e);
        }
    }
}
