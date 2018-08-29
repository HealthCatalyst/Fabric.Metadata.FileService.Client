namespace Fabric.Metadata.FileService.Client.Tests
{
    using System;
    using System.Net;
    using System.Net.Http;
    using System.Net.Http.Headers;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;
    using Moq.Protected;
    using Newtonsoft.Json;

    [TestClass]
    public class FileServiceClientTests
    {
        [TestMethod]
        public async Task TestPlanHttpClient()
        {
            var requestUri = new Uri("http://google.com");
            var expectedResponse = "Response text";

            var mockResponse = new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(expectedResponse) };

            var handlerMock = new Mock<HttpMessageHandler>(MockBehavior.Strict);
            handlerMock
                .Protected()
                // Setup the PROTECTED method to mock
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>()
                )
                // prepare the expected response of the mocked http call
                .ReturnsAsync(new HttpResponseMessage()
                {
                    StatusCode = HttpStatusCode.OK,
                    Content = new StringContent(expectedResponse),
                })
                .Verifiable();

            var httpClient = new HttpClient(handlerMock.Object);
            var result = await httpClient.GetStringAsync(requestUri).ConfigureAwait(false);
            Assert.AreEqual(expectedResponse, result);
        }

        [TestMethod]
        public async Task CheckFileAsyncSuccess()
        {
            // arrange
            var resourceId = 1;
            var baseUri = new Uri("http://foo/");
            var fullUri = new Uri(baseUri, $"Files({resourceId})");

            var mockResponse = new HttpResponseMessage(HttpStatusCode.OK)
            {
                StatusCode = HttpStatusCode.NoContent,
                Content = new StringContent(string.Empty),
            };

            mockResponse.Content.Headers.LastModified = DateTimeOffset.UtcNow;
            var myHash = "MyHash";
            mockResponse.Content.Headers.ContentMD5 = Encoding.UTF8.GetBytes(myHash);
            var myFileName = "MyFile.txt";
            mockResponse.Content.Headers.ContentDisposition =
                new ContentDispositionHeaderValue("attachment")
                {
                    FileName = myFileName
                };

            var handlerMock = new Mock<HttpMessageHandler>(MockBehavior.Strict);
            handlerMock
                .Protected()
                // Setup the PROTECTED method to mock
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>()
                )
                // prepare the expected response of the mocked http call
                .ReturnsAsync(mockResponse)
                .Verifiable();

            string accessToken = "MyAccessToken";

            // act
            var fileServiceClient = new FileServiceClient(accessToken, baseUri.ToString(), handlerMock.Object);
            var result = await fileServiceClient.CheckFileAsync(resourceId).ConfigureAwait(false);

            // assert
            Assert.AreEqual(mockResponse.StatusCode, result.StatusCode);
            Assert.AreEqual(mockResponse.Content.Headers.LastModified, result.LastModified);
            Assert.AreEqual(myHash, result.HashForFileOnServer);
            Assert.AreEqual(myFileName, result.FileNameOnServer);

            handlerMock.Protected()
                .Verify(
                    "SendAsync",
                    Times.Once(),
                    ItExpr.Is<HttpRequestMessage>(
                        req => req.Method == HttpMethod.Head
                               && req.RequestUri == fullUri),
                    ItExpr.IsAny<CancellationToken>());
        }

        [TestMethod]
        public async Task HandlesCheckFileAsyncWithNetworkHiccup()
        {
            // arrange
            var resourceId = 1;
            var baseUri = new Uri("http://foo/");
            var fullUri = new Uri(baseUri, $"Files({resourceId})");

            var mockResponse = new HttpResponseMessage(HttpStatusCode.OK)
            {
                StatusCode = HttpStatusCode.NoContent,
                Content = new StringContent(string.Empty),
            };

            var headersLastModified = DateTimeOffset.UtcNow;

            mockResponse.Content.Headers.LastModified = headersLastModified;
            var myHash = "MyHash";
            mockResponse.Content.Headers.ContentMD5 = Encoding.UTF8.GetBytes(myHash);
            var myFileName = "MyFile.txt";
            mockResponse.Content.Headers.ContentDisposition =
                new ContentDispositionHeaderValue("attachment")
                {
                    FileName = myFileName
                };

            int count = 0;

            var handlerMock = new Mock<HttpMessageHandler>(MockBehavior.Strict);
            handlerMock
                .Protected()
                // Setup the PROTECTED method to mock
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>()
                )
                // prepare the expected response of the mocked http call
                .ReturnsAsync(() =>
                {
                    if (count < 1)
                    {
                        count++;
                        return new HttpResponseMessage(HttpStatusCode.OK)
                        {
                            StatusCode = HttpStatusCode.InternalServerError,
                            Content = new StringContent("This is my error"),
                        };
                    }
                    return mockResponse;
                })
                .Verifiable();

            string accessToken = "MyAccessToken";

            // act
            var fileServiceClient = new FileServiceClient(accessToken, baseUri.ToString(), handlerMock.Object);
            fileServiceClient.TransientError +=
                (sender, args) => Console.WriteLine("Transient Error: " + args.ResultStatusCode + " " + args.Content);

            var result = await fileServiceClient.CheckFileAsync(resourceId).ConfigureAwait(false);

            // assert
            Assert.AreEqual(mockResponse.StatusCode, result.StatusCode);
            Assert.AreEqual(headersLastModified, result.LastModified);
            Assert.AreEqual(myHash, result.HashForFileOnServer);
            Assert.AreEqual(myFileName, result.FileNameOnServer);

            handlerMock.Protected()
                .Verify(
                    "SendAsync",
                    Times.Exactly(2),
                    ItExpr.Is<HttpRequestMessage>(
                        req => req.Method == HttpMethod.Head
                               && req.RequestUri == fullUri),
                    ItExpr.IsAny<CancellationToken>());
        }
    }
}
