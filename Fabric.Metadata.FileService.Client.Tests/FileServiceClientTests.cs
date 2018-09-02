namespace Fabric.Metadata.FileService.Client.Tests
{
    using System;
    using System.Net;
    using System.Net.Http;
    using System.Net.Http.Headers;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Interfaces;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;
    using Moq.Protected;

    [TestClass]
    public class FileServiceClientTests
    {
        private string accessToken;
        private Mock<IAccessTokenRepository> mockAccessTokenRepository;

        [TestInitialize]
        public void TestInitialize()
        {
            this.accessToken = "MyAccessToken";

            this.mockAccessTokenRepository = new Mock<IAccessTokenRepository>();
            mockAccessTokenRepository.Setup(
                    service => service.GetAccessTokenAsync())
                .ReturnsAsync(accessToken);
        }

        [TestMethod]
        public async Task HttpClientWorks()
        {
            var requestUri = new Uri("http://google.com");
            var expectedResponse = "Response text";

            var mockHttpMessageHandler = new Mock<HttpMessageHandler>(MockBehavior.Strict);
            mockHttpMessageHandler
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

            var httpClient = new HttpClient(mockHttpMessageHandler.Object);
            var result = await httpClient.GetStringAsync(requestUri).ConfigureAwait(false);
            Assert.AreEqual(expectedResponse, result);
        }

        [TestMethod]
        public async Task CheckFileAsyncPasses()
        {
            // arrange
            FileServiceClient.ClearHttpClient();

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

            // act
            var fileServiceClient = new FileServiceClient(this.mockAccessTokenRepository.Object, baseUri.ToString(), handlerMock.Object);
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
                        req => req.Method == HttpMethod.Get
                               && req.RequestUri == fullUri),
                    ItExpr.IsAny<CancellationToken>());
        }

        [TestMethod]
        public async Task HandlesCheckFileAsyncWithNetworkHiccup()
        {
            // arrange
            FileServiceClient.ClearHttpClient();

            var resourceId = 1;
            var baseUri = new Uri("http://foo/");
            var fullUri = new Uri(baseUri, $"Files({resourceId})");

            var mockResponse = new HttpResponseMessage(HttpStatusCode.OK)
            {
                StatusCode = HttpStatusCode.NoContent,
                Content = new StringContent(string.Empty),
            };

            var headersLastModified = DateTimeOffset.Parse("10/20/2018");

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
                        var response = new HttpResponseMessage(HttpStatusCode.OK)
                        {
                            StatusCode = HttpStatusCode.InternalServerError,
                            Content = new StringContent("This is my error"),
                        };
                        response.Content.Headers.LastModified = DateTimeOffset.Parse("11/11/2017");
                        return response;
                    }
                    return mockResponse;
                })
                .Verifiable();

            // act
            var fileServiceClient = new FileServiceClient(this.mockAccessTokenRepository.Object, baseUri.ToString(), handlerMock.Object);
            fileServiceClient.TransientError +=
                (sender, args) => Console.WriteLine("Transient Error: " + args.ResultStatusCode + " " + args.Content);

            var result = await fileServiceClient.CheckFileAsync(resourceId).ConfigureAwait(false);

            // assert
            Assert.AreEqual(mockResponse.StatusCode, result.StatusCode);
            Assert.AreEqual(myHash, result.HashForFileOnServer);
            Assert.AreEqual(myFileName, result.FileNameOnServer);
            Assert.AreEqual(headersLastModified, result.LastModified);

            handlerMock.Protected()
                .Verify(
                    "SendAsync",
                    Times.Exactly(2),
                    ItExpr.Is<HttpRequestMessage>(
                        req => req.Method == HttpMethod.Get
                               && req.RequestUri == fullUri),
                    ItExpr.IsAny<CancellationToken>());
        }
    }
}
