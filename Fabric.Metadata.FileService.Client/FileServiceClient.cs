﻿namespace Fabric.Metadata.FileService.Client
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
    using Interfaces;
    using Polly;
    using Structures;

    /// <inheritdoc />
    /// <summary>
    /// This class manages the communication with the DOS File Service
    /// </summary>
    public class FileServiceClient : IFileServiceClient
    {
        public event NavigatingEventHandler Navigating;
        public event NavigatedEventHandler Navigated;
        public event TransientErrorEventHandler TransientError;

        private const string DispositionType = "attachment";
        private const string ApplicationJsonMediaType = "application/json";
        private const string ApplicationOctetStreamMediaType = "application/octet-stream";
        private const int SecondsBetweenRetries = 2;
        private const int MaxRetryCount = 3;

        // make HttpClient static per https://aspnetmonsters.com/2016/08/2016-08-27-httpclientwrong/
        private static HttpClient _httpClient;

        private readonly string mdsBaseUrl;

        public FileServiceClient(string accessToken, string mdsBaseUrl, HttpMessageHandler httpClientHandler)
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

            if (_httpClient == null)
            {
                _httpClient = this.CreateHttpClient(accessToken, httpClientHandler);
            }

            this.mdsBaseUrl = mdsBaseUrl;
        }

        public static void ClearHttpClient()
        {
            _httpClient = null;
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

            var method = Convert.ToString(HttpMethod.Get);

            OnNavigating(new NavigatingEventArgs(resourceId, fullUri, method));

            var httpResponse = await Policy
                .HandleResult<HttpResponseMessage>(message =>
                    message.StatusCode != HttpStatusCode.NoContent && message.StatusCode != HttpStatusCode.NotFound)
                .WaitAndRetryAsync(MaxRetryCount, i => TimeSpan.FromSeconds(SecondsBetweenRetries),
                    async (result, timeSpan, retryCount, context) =>
                    {
                        var errorContent = await result.Result.Content.ReadAsStringAsync();
                        OnTransientError(new TransientErrorEventArgs(method, fullUri, result.Result.StatusCode.ToString(), errorContent));
                    })
                .ExecuteAsync(() =>
                {
                    var httpRequestMessage = new HttpRequestMessage(HttpMethod.Get, fullUri);
                    return _httpClient.SendAsync(httpRequestMessage);
                });

            var content = await httpResponse.Content.ReadAsStringAsync();
            OnNavigated(
                new NavigatedEventArgs(resourceId, method, fullUri, httpResponse.StatusCode.ToString(), content));

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
                {
                    // this is acceptable response if the file does not exist on the server
                    return new CheckFileResult
                    {
                        StatusCode = httpResponse.StatusCode,
                        FullUri = fullUri
                    };
                }
                default:
                {
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

            var httpResponse = await Policy
                .HandleResult<HttpResponseMessage>(message =>
                    message.StatusCode != HttpStatusCode.OK && message.StatusCode != HttpStatusCode.BadRequest)
                .WaitAndRetryAsync(MaxRetryCount, i => TimeSpan.FromSeconds(SecondsBetweenRetries),
                    async (result, timeSpan, retryCount, context) =>
                    {
                        var errorContent = await result.Result.Content.ReadAsStringAsync();
                        OnTransientError(new TransientErrorEventArgs(method, fullUri, result.Result.StatusCode.ToString(), errorContent));
                    })
                .ExecuteAsync(() => _httpClient.PostAsync(
                    fullUri,
                    new StringContent(JsonConvert.SerializeObject(form),
                        Encoding.UTF8,
                        ApplicationJsonMediaType)));

            var content = await httpResponse.Content.ReadAsStringAsync();
            OnNavigated(new NavigatedEventArgs(resourceId, method, fullUri, httpResponse.StatusCode.ToString(), content));

            switch (httpResponse.StatusCode)
            {
                case HttpStatusCode.OK:
                    {
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
        /// This method calls PUT Files({resourceId})/UploadSessions({sessionId})
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
            var fullUri = new Uri(baseUri, $"Files({resourceId})/UploadSessions({sessionId})");

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

                var httpResponse = await Policy
                    .HandleResult<HttpResponseMessage>(message =>
                        message.StatusCode != HttpStatusCode.OK && message.StatusCode != HttpStatusCode.BadRequest)
                    .WaitAndRetryAsync(MaxRetryCount, i => TimeSpan.FromSeconds(SecondsBetweenRetries),
                        async (result, timeSpan, retryCount, context) =>
                        {
                            var errorContent = await result.Result.Content.ReadAsStringAsync();
                            OnTransientError(new TransientErrorEventArgs(method, fullUri, result.Result.StatusCode.ToString(), errorContent));
                        })
                    // ReSharper disable once AccessToDisposedClosure
                    .ExecuteAsync(() => _httpClient.PutAsync(fullUri, requestContent));

                var content = await httpResponse.Content.ReadAsStringAsync();
                OnNavigated(new NavigatedEventArgs(resourceId, method, fullUri, httpResponse.StatusCode.ToString(), content ));

                switch (httpResponse.StatusCode)
                {
                    case HttpStatusCode.OK:
                    {
                        return new UploadStreamResult
                        {
                            StatusCode = httpResponse.StatusCode,
                            // ReSharper disable once RedundantAssignment
                            PartsUploaded = numPartsUploaded++
                        };
                    }
                    default:
                    {
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

            var httpResponse = await Policy
                .HandleResult<HttpResponseMessage>(message =>
                    message.StatusCode != HttpStatusCode.OK && message.StatusCode != HttpStatusCode.BadRequest)
                .WaitAndRetryAsync(MaxRetryCount, i => TimeSpan.FromSeconds(SecondsBetweenRetries),
                    async (result, timeSpan, retryCount, context) =>
                    {
                        var errorContent = await result.Result.Content.ReadAsStringAsync();
                        OnTransientError(new TransientErrorEventArgs(method, fullUri, result.Result.StatusCode.ToString(), errorContent));
                    })
                .ExecuteAsync(() => _httpClient.PostAsync(
                    fullUri,
                    new StringContent(JsonConvert.SerializeObject(form),
                        Encoding.UTF8,
                        ApplicationJsonMediaType)));

            var content = await httpResponse.Content.ReadAsStringAsync();

            OnNavigated(new NavigatedEventArgs(resourceId, method, fullUri, httpResponse.StatusCode.ToString(), content));

            switch (httpResponse.StatusCode)
            {
                case HttpStatusCode.OK:
                {
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
                    dynamic clientResponse = JsonConvert.DeserializeObject(content);
                    var errorCode = clientResponse["ErrorCode"] != null
                        ? Convert.ToString(clientResponse["ErrorCode"])
                        : null;

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

            var httpResponse = await Policy
                .HandleResult<HttpResponseMessage>(message =>
                    message.StatusCode != HttpStatusCode.OK && message.StatusCode != HttpStatusCode.BadRequest)
                .WaitAndRetryAsync(MaxRetryCount, i => TimeSpan.FromSeconds(SecondsBetweenRetries),
                    async (result, timeSpan, retryCount, context) =>
                    {
                        var errorContent = await result.Result.Content.ReadAsStringAsync();
                        OnTransientError(new TransientErrorEventArgs(method, fullUri, result.Result.StatusCode.ToString(), errorContent));
                    })
                .ExecuteAsync(() =>
                {
                    var httpRequestMessage = new HttpRequestMessage(HttpMethod.Get, fullUri);
                    httpRequestMessage.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("*/*"));
                    return _httpClient.SendAsync(httpRequestMessage);
                });

            // we don't want to write the content in this case as the file can be very large
            OnNavigated(
                new NavigatedEventArgs(resourceId, method, fullUri, httpResponse.StatusCode.ToString(), string.Empty));

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
                            new FileStream(fullPath, FileMode.Create, FileAccess.Write, FileShare.None,
                                bufferSize: Convert.ToInt32(bufferSize), useAsync: true))
                        {
                            await contentStream.CopyToAsync(destinationStream);
                        }
                    }

                    return result;
                }
                case HttpStatusCode.NotFound:
                {
                    // this is acceptable response if the file does not exist on the server
                    return new CheckFileResult
                    {
                        StatusCode = httpResponse.StatusCode,
                        FullUri = fullUri
                    };
                }
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

            var httpResponse = await Policy
                .HandleResult<HttpResponseMessage>(message =>
                    message.StatusCode != HttpStatusCode.OK && message.StatusCode != HttpStatusCode.BadRequest)
                .WaitAndRetryAsync(MaxRetryCount, i => TimeSpan.FromSeconds(SecondsBetweenRetries),
                    async (result, timeSpan, retryCount, context) =>
                    {
                        var errorContent = await result.Result.Content.ReadAsStringAsync();
                        OnTransientError(new TransientErrorEventArgs(method, fullUri, result.Result.StatusCode.ToString(), errorContent));
                    })
                .ExecuteAsync(() => _httpClient.DeleteAsync(fullUri));

            var content = await httpResponse.Content.ReadAsStringAsync();

            OnNavigated(new NavigatedEventArgs(resourceId, method, fullUri, httpResponse.StatusCode.ToString(), content));

            switch (httpResponse.StatusCode)
            {
                case HttpStatusCode.OK:
                {
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
        }

        private HttpClient CreateHttpClient(string accessToken, HttpMessageHandler httpClientHandler)
        {
            if (accessToken == null) throw new ArgumentNullException(nameof(accessToken));

            var createHttpClient = new HttpClient(httpClientHandler);
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
        private void OnTransientError(TransientErrorEventArgs e)
        {
            TransientError?.Invoke(this, e);
        }
    }
}
