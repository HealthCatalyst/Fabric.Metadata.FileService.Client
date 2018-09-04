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
    using Exceptions;
    using Interfaces;
    using Polly;
    using Polly.Retry;
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
        public event AccessTokenRequestedEventHandler AccessTokenRequested;
        public event NewAccessTokenRequestedEventHandler NewAccessTokenRequested;

        private const int DefaultBufferSize = 4096;
        private const string DispositionType = "attachment";
        private const string ApplicationJsonMediaType = "application/json";
        private const string ApplicationOctetStreamMediaType = "application/octet-stream";
        private const int SecondsBetweenRetries = 2;
        private const int MaxRetryCount = 3;

        // make HttpClient static per https://aspnetmonsters.com/2016/08/2016-08-27-httpclientwrong/
        private static HttpClient _httpClient;
        private readonly IAccessTokenRepository accessTokenRepository;
        private readonly Uri mdsBaseUrl;
        private int numberOfPartsUploaded;

        private readonly HttpStatusCode[] httpStatusCodesWorthRetrying = {
            HttpStatusCode.Unauthorized, // 401
            HttpStatusCode.RequestTimeout, // 408
            HttpStatusCode.InternalServerError, // 500
            HttpStatusCode.BadGateway, // 502
            HttpStatusCode.ServiceUnavailable, // 503
            HttpStatusCode.GatewayTimeout, // 504
            HttpStatusCode.Conflict, // 409
        };

        public static TimeSpan HttpTimeout = TimeSpan.FromMinutes(5);

        public FileServiceClient(IAccessTokenRepository accessTokenRepository, Uri mdsBaseUrl, HttpMessageHandler httpClientHandler)
        {
            if (accessTokenRepository == null)
            {
                throw new ArgumentNullException(nameof(accessTokenRepository));
            }

            if (string.IsNullOrWhiteSpace(mdsBaseUrl.ToString()))
            {
                throw new ArgumentNullException(nameof(mdsBaseUrl));
            }

            if (!mdsBaseUrl.ToString().EndsWith(@"/"))
            {
                mdsBaseUrl = new Uri($@"{mdsBaseUrl}/");
            }

            if (!mdsBaseUrl.IsWellFormedOriginalString())
            {
                throw new InvalidOperationException($"MDS Url, '{mdsBaseUrl}' is not a well formed url.");
            }

            this.accessTokenRepository = accessTokenRepository;
            this.mdsBaseUrl = mdsBaseUrl;

            if (_httpClient == null)
            {
                _httpClient = CreateHttpClient(httpClientHandler);
            }

        }

        public static void ClearHttpClient()
        {
            _httpClient = null;
        }
        /// <inheritdoc />
        /// <summary>
        /// This calls HEAD Files({resourceId})
        /// </summary>
        /// <param name="resourceId"></param>
        /// <returns></returns>
        public async Task<CheckFileResult> CheckFileAsync(int resourceId)
        {
            if (resourceId <= 0) throw new ArgumentOutOfRangeException(nameof(resourceId));

            var fullUri = new Uri(mdsBaseUrl, $"Files({resourceId})");

            var method = Convert.ToString(HttpMethod.Get);

            await this.SetAuthorizationHeaderInHttpClientAsync(resourceId);

            OnNavigating(new NavigatingEventArgs(resourceId, method, fullUri));

            var policy = GetRetryPolicy(resourceId, method, fullUri);

            var httpResponse = await policy
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
                case HttpStatusCode.OK:
                    {
                        throw new InvalidOperationException($"The url, {fullUri}, sent back the whole file instead of just the headers.  You may be running an old version of MDS. Please install the latest version of MDS from the Installer.");
                    }

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

        /// <inheritdoc />
        /// <summary>
        /// This calls POST Files({resourceId})/UploadSessions
        /// </summary>
        /// <param name="resourceId"></param>
        /// <returns></returns>
        public async Task<CreateSessionResult> CreateNewUploadSessionAsync(int resourceId)
        {
            if (resourceId <= 0) throw new ArgumentOutOfRangeException(nameof(resourceId));

            var fullUri = new Uri(this.mdsBaseUrl, $"Files({resourceId})/UploadSessions");

            var method = Convert.ToString(HttpMethod.Post);

            await this.SetAuthorizationHeaderInHttpClientAsync(resourceId);

            OnNavigating(new NavigatingEventArgs(resourceId, method, fullUri));

            var form = new
            {

            };

            var policy = GetRetryPolicy(resourceId, method, fullUri);

            var httpResponse = await policy
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

        /// <inheritdoc />
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
        /// <returns></returns>
        public async Task<UploadStreamResult> UploadStreamAsync(int resourceId,
            Guid sessionId,
            Stream stream,
            FilePart filePart,
            string fileName,
            long fullFileSize,
            int filePartsCount)
        {
            if (resourceId <= 0) throw new ArgumentOutOfRangeException(nameof(resourceId));
            if (filePartsCount < 0) throw new ArgumentOutOfRangeException(nameof(filePartsCount));

            var fullUri = new Uri(this.mdsBaseUrl, $"Files({resourceId})/UploadSessions({sessionId})");

            var method = Convert.ToString(HttpMethod.Put);

            var policy = GetRetryPolicy(resourceId, method, fullUri);

            await this.SetAuthorizationHeaderInHttpClientAsync(resourceId);

            OnNavigating(new NavigatingEventArgs(resourceId, method, fullUri));

            var httpResponse = await policy
                .ExecuteAsync(async () =>
                {
                    using (var requestContent = new MultipartFormDataContent())
                    {
                        // StreamContent disposes the stream when it is done so we need to keep a copy for retries
                        var memoryStream = new MemoryStream();
                        // ReSharper disable once AccessToDisposedClosure
                        stream.Seek(0, SeekOrigin.Begin);
                        // ReSharper disable once AccessToDisposedClosure
                        await stream.CopyToAsync(memoryStream);

                        var fileContent = new StreamContent(memoryStream);

                        fileContent.Headers.ContentDisposition = new
                            ContentDispositionHeaderValue(DispositionType)
                            {
                                FileName = fileName
                            };

                        fileContent.Headers.ContentRange =
                            new ContentRangeHeaderValue(filePart.Offset, filePart.Offset + filePart.Size - 1,
                                fullFileSize);

                        fileContent.Headers.ContentType = new MediaTypeHeaderValue(ApplicationOctetStreamMediaType);
                        fileContent.Headers.ContentMD5 = Encoding.UTF8.GetBytes(filePart.Hash);
                        requestContent.Add(fileContent);

                        return await _httpClient.PutAsync(fullUri, requestContent);
                    }
                });

            var content = await httpResponse.Content.ReadAsStringAsync();
            OnNavigated(new NavigatedEventArgs(resourceId, method, fullUri, httpResponse.StatusCode.ToString(), content));

            switch (httpResponse.StatusCode)
            {
                case HttpStatusCode.OK:
                    {
                        this.numberOfPartsUploaded++;

                        return new UploadStreamResult
                        {
                            StatusCode = httpResponse.StatusCode,
                            PartsUploaded = this.numberOfPartsUploaded
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

        /// <inheritdoc />
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

            var fullUri = new Uri(this.mdsBaseUrl, $"Files({resourceId})/UploadSessions({sessionId})/MetadataService.Commit");

            var method = Convert.ToString(HttpMethod.Post);

            await this.SetAuthorizationHeaderInHttpClientAsync(resourceId);

            OnNavigating(new NavigatingEventArgs(resourceId, method, fullUri));

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

            var policy = GetRetryPolicy(resourceId, method, fullUri);

            var httpResponse = await policy
                .ExecuteAsync(() =>
                {
                    var jsonSerializerSettings = new JsonSerializerSettings
                    {
                        NullValueHandling = NullValueHandling.Ignore
                    };
                    return _httpClient.PostAsync(
                        fullUri,
                        new StringContent(JsonConvert.SerializeObject(form, jsonSerializerSettings),
                            Encoding.UTF8,
                            ApplicationJsonMediaType));
                });

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

        /// <inheritdoc />
        /// <summary>
        /// This calls GET Files({resourceId})
        /// </summary>
        /// <param name="resourceId"></param>
        /// <param name="utTempPath"></param>
        /// <returns></returns>
        public async Task<CheckFileResult> DownloadFileAsync(int resourceId, string utTempPath)
        {
            if (resourceId <= 0) throw new ArgumentOutOfRangeException(nameof(resourceId));

            var fullUri = new Uri(this.mdsBaseUrl, $"Files({resourceId})");

            var method = Convert.ToString(HttpMethod.Get);

            await this.SetAuthorizationHeaderInHttpClientAsync(resourceId);

            OnNavigating(new NavigatingEventArgs(resourceId, method, fullUri));

            var policy = GetRetryPolicy(resourceId, method, fullUri);

            var httpResponse = await policy
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

                        if (contentDispositionFileName != null)
                        {
                            var fullPath = Path.Combine(utTempPath, contentDispositionFileName);
                            using (FileStream destinationStream =
                                new FileStream(fullPath, FileMode.Create, FileAccess.Write, FileShare.None,
                                    bufferSize: DefaultBufferSize, useAsync: true))
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

        /// <inheritdoc />
        /// <summary>
        /// This calls DELETE Files({resourceId})/UploadSessions
        /// </summary>
        /// <param name="resourceId"></param>
        /// <returns></returns>
        public async Task<DeleteSessionResult> DeleteUploadSessionAsync(int resourceId)
        {
            if (resourceId <= 0) throw new ArgumentOutOfRangeException(nameof(resourceId));

            var fullUri = new Uri(this.mdsBaseUrl, $"Files({resourceId})/UploadSessions");

            var method = Convert.ToString(HttpMethod.Delete);

            await this.SetAuthorizationHeaderInHttpClientAsync(resourceId);

            OnNavigating(new NavigatingEventArgs(resourceId, method, fullUri));

            var policy = GetRetryPolicy(resourceId, method, fullUri);

            var httpResponse = await policy
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

        private static HttpClient CreateHttpClient(HttpMessageHandler httpClientHandler)
        {
            var httpClient = new HttpClient(httpClientHandler, false);
            httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue(ApplicationJsonMediaType));
            httpClient.Timeout = HttpTimeout;
            return httpClient;
        }

        private async Task SetAuthorizationHeaderInHttpClientAsync(int resourceId)
        {
            OnAccessTokenRequested(new AccessTokenRequestedEventArgs(resourceId));

            var accessToken = await accessTokenRepository.GetAccessTokenAsync();
            if (string.IsNullOrWhiteSpace(accessToken))
            {
                throw new InvalidAccessTokenException(accessToken);
            }

            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        }
        private async Task SetAuthorizationHeaderInHttpClientWithNewBearerTokenAsync(int resourceId)
        {
            OnNewAccessTokenRequested(new NewAccessTokenRequestedEventArgs(resourceId));

            var accessToken = await accessTokenRepository.GetNewAccessTokenAsync();
            if (string.IsNullOrWhiteSpace(accessToken))
            {
                throw new InvalidAccessTokenException(accessToken);
            }

            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        }

        private RetryPolicy<HttpResponseMessage> GetRetryPolicy(int resourceId, string method, Uri fullUri)
        {
            return Policy
                .Handle<HttpRequestException>()
                .Or<TaskCanceledException>()
                .OrResult<HttpResponseMessage>(message => httpStatusCodesWorthRetrying.Contains(message.StatusCode))
                .WaitAndRetryAsync(MaxRetryCount, i => TimeSpan.FromSeconds(SecondsBetweenRetries),
                    async (result, timeSpan, retryCount, context) =>
                    {
                        if (result.Result != null)
                        {
                            if (result.Result.StatusCode == HttpStatusCode.Unauthorized)
                            {
                                await this.SetAuthorizationHeaderInHttpClientWithNewBearerTokenAsync(resourceId);
                            }

                            var errorContent = await result.Result.Content.ReadAsStringAsync();
                            OnTransientError(new TransientErrorEventArgs(resourceId, method, fullUri,
                                result.Result.StatusCode.ToString(), errorContent, retryCount, MaxRetryCount));
                        }
                        else
                        {
                            OnTransientError(new TransientErrorEventArgs(resourceId, method, fullUri, "Exception",
                                result.Exception?.ToString(), retryCount, MaxRetryCount));
                        }
                    });
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

        private void OnAccessTokenRequested(AccessTokenRequestedEventArgs e)
        {
            AccessTokenRequested?.Invoke(this, e);
        }

        private void OnNewAccessTokenRequested(NewAccessTokenRequestedEventArgs e)
        {
            NewAccessTokenRequested?.Invoke(this, e);
        }
    }
}
