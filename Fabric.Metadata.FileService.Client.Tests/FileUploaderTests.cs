namespace Fabric.Metadata.FileService.Client.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using System.IO;
    using System.Net;
    using FileServiceResults;
    using Interfaces;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;
    using Structures;

    [TestClass]
    public class FileUploaderTests
    {
        [TestMethod]
        public async Task UploadFileIsSuccessful()
        {
            // arrange
            var mockFileService = new Mock<IFileServiceClient>();

            var mockFileServiceFactory = new Mock<IFileServiceClientFactory>();

            mockFileServiceFactory.Setup(
                    service => service.CreateFileServiceClient(It.IsAny<string>(), It.IsAny<string>()))
                .Returns(mockFileService.Object);

            var fileUploader = new FileUploader(mockFileServiceFactory.Object);

            var fileName = "foo.txt";
            string filePath = Path.Combine(Path.GetTempPath(), fileName);
            File.WriteAllText(filePath, "123");
            long fullFileSize = new FileInfo(filePath).Length;
            var hashForFile = new MD5FileHasher().CalculateHashForFile(filePath);

            string accessToken = "myAccessToken";
            int resourceId = 1;
            string mdsBaseUrl = "http://foo";

            var checkFileResult = new CheckFileResult
            {
                StatusCode = HttpStatusCode.NoContent,
                LastModified = DateTimeOffset.UtcNow,
                FileNameOnServer = fileName,
                HashForFileOnServer = "MyHash"
            };

            mockFileService.Setup(
                    service => service.CheckFileAsync(resourceId))
                .ReturnsAsync(checkFileResult);

            var sessionId = Guid.NewGuid();
            var uploadSession = new UploadSession
            {
                SessionId = sessionId,
                FileUploadChunkSizeInBytes = 1,
                FileUploadMaxFileSizeInMegabytes = 10
            };
            var createSessionResult = new CreateSessionResult
            {
                StatusCode = HttpStatusCode.OK,
                Session = uploadSession
            };
            mockFileService.Setup(
                    service => service.CreateNewUploadSessionAsync(resourceId))
                .ReturnsAsync(createSessionResult);

            var fileSplitter = new FileSplitter();
            var countOfFileParts = fileSplitter.GetCountOfFileParts(createSessionResult.Session.FileUploadChunkSizeInBytes, fullFileSize);

            var uploadStreamResult = new UploadStreamResult
            {
                StatusCode = HttpStatusCode.OK,
                PartsUploaded = 1
            };

            mockFileService.Setup(
                    service => service.UploadStreamAsync(resourceId, sessionId,
                        It.IsAny<Stream>(), It.IsAny<FilePart>(), fileName, fullFileSize, countOfFileParts,
                        It.IsAny<int>()))
                .ReturnsAsync(uploadStreamResult);

            var commitResult = new CommitResult
            {
                StatusCode = HttpStatusCode.OK,
                Session = uploadSession
            };

            mockFileService.Setup(
                    service => service.CommitAsync(resourceId, sessionId, fileName, hashForFile, fullFileSize,
                        It.IsAny<IList<FilePart>>()))
                .ReturnsAsync(commitResult);

            // act
            await fileUploader.UploadFileAsync(filePath, accessToken, resourceId, mdsBaseUrl);

            // assert
            mockFileService.Verify(
                service => service.CheckFileAsync(1),
                Times.Once);

            mockFileService.Verify(
                service => service.CreateNewUploadSessionAsync(resourceId),
                Times.Once);

            mockFileService.Verify(
                service => service.UploadStreamAsync(resourceId, sessionId,
                    It.IsAny<Stream>(), It.IsAny<FilePart>(), fileName, fullFileSize, countOfFileParts,
                    It.IsAny<int>()),
                Times.Exactly(countOfFileParts));

            mockFileService.Verify(
                service => service.CommitAsync(resourceId, sessionId, fileName, hashForFile, fullFileSize,
                    It.IsAny<IList<FilePart>>()),
                Times.Once);
        }
    }
}
