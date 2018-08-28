using System;
using System.Collections.Generic;
using System.Text;

namespace Fabric.Metadata.FileService.Client
{
    using System.Net;
    using System.Net.Http;
    using System.Net.Http.Headers;
    using System.Threading.Tasks;
    using Events;

    public class FileServiceClient : IDisposable
    {
        public event NavigatingEventHandler Navigating;
        public event NavigatedEventHandler Navigated;

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

            this.httpClient = this.CreateHttpClient(accessToken);
            this.mdsBaseUrl = mdsBaseUrl;
        }

        public async Task<CheckFileResult> CheckFile(int resourceId)
        {
            var baseUri = new Uri(mdsBaseUrl);
            var fullUri = new Uri(baseUri, $"Files({resourceId})");


            var method = Convert.ToString(HttpMethod.Head);
            OnNavigating(new NavigatingEventArgs(resourceId, fullUri, method));

            var httpResponse = await httpClient.SendAsync(new HttpRequestMessage(HttpMethod.Head, fullUri));

            OnNavigated(new NavigatedEventArgs(resourceId, method, fullUri, httpResponse.StatusCode.ToString()));

            if (httpResponse.StatusCode == HttpStatusCode.NoContent)
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

                return result;
            }
            else if (httpResponse.StatusCode == HttpStatusCode.NotFound)
            {
                // this is acceptable response if the file does not exist on the server
                return new CheckFileResult
                {
                    StatusCode = httpResponse.StatusCode,
                    FullUri = fullUri
                };
            }
            else
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

        public void Dispose()
        {
            this.httpClient.Dispose();
        }

        private HttpClient CreateHttpClient(string accessToken)
        {
            if (accessToken == null) throw new ArgumentNullException(nameof(accessToken));

            var createHttpClient = new HttpClient();
            createHttpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
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

        public class FileServiceResult
        {
            public HttpStatusCode StatusCode { get; set; }
            public string Error { get; set; }
            public Uri FullUri { get; set; }
        }

        public class CheckFileResult : FileServiceResult
        {
            public DateTimeOffset? LastModified { get; set; }
            public string FileNameOnServer { get; set; }
            public string HashForFileOnServer { get; set; }
        }
    }
}
