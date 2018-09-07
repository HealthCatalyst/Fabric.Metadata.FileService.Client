namespace Fabric.Metadata.FileService.Client.Tests
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Net;
    using System.Threading;
    using System.Threading.Tasks;
    using FileServiceResults;
    using Interfaces;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;
    using Structures;
    using Utils;

    [TestClass]
    public class WorkflowTests
    {
        private string accessToken;
        private FileUploader classUnderTest;
        private Mock<IFileServiceClient> mockFileService;
        private Uri mdsBaseUrl;
        private CancellationToken cancellationToken;

        [TestInitialize]
        public void TestInitialize()
        {
            this.accessToken = "myAccessToken";
            this.mdsBaseUrl = new Uri("http://foo");

            this.mockFileService = new Mock<IFileServiceClient>();

            var mockFileServiceFactory = new Mock<IFileServiceClientFactory>();

            this.cancellationToken = new CancellationToken();

            var mockAccessTokenRepository = new Mock<IFileServiceAccessTokenRepository>();
            mockAccessTokenRepository.Setup(
                    service => service.GetAccessTokenAsync())
                .ReturnsAsync(accessToken);

            mockFileServiceFactory.Setup(
                    service => service.CreateFileServiceClient(It.IsAny<IFileServiceAccessTokenRepository>(), It.IsAny<Uri>(), this.cancellationToken))
                .Returns(mockFileService.Object);

            this.classUnderTest = new FileUploader(mockFileServiceFactory.Object, mockAccessTokenRepository.Object, this.mdsBaseUrl);
        }

        [TestMethod]
        public async Task UploadsWhenThereIsNoExistingFile()
        {
            // arrange
            var fileName = "foo.txt";
            string filePath = Path.Combine(Path.GetTempPath(), fileName);
            File.WriteAllText(filePath, "123");
            long fullFileSize = new FileInfo(filePath).Length;
            var hashForFile = new MD5FileHasher().CalculateHashForFile(filePath);

            int resourceId = 1;
            var checkFileResult = new CheckFileResult
            {
                StatusCode = HttpStatusCode.NotFound,
            };

            // Have CheckFileAsync return NotFound with a hash
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

            // Crete Update 
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
                        It.IsAny<Stream>(), It.IsAny<FilePart>(), fileName, fullFileSize, countOfFileParts))
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
            await this.classUnderTest.UploadFileAsync(resourceId, filePath, this.cancellationToken);

            // assert
            mockFileService.Verify(
                service => service.CheckFileAsync(1),
                Times.Once);

            mockFileService.Verify(
                service => service.CreateNewUploadSessionAsync(resourceId),
                Times.Once);

            mockFileService.Verify(
                service => service.UploadStreamAsync(resourceId, sessionId,
                    It.IsAny<Stream>(), It.IsAny<FilePart>(), fileName, fullFileSize, countOfFileParts),
                Times.Exactly(countOfFileParts));

            mockFileService.Verify(
                service => service.CommitAsync(resourceId, sessionId, fileName, hashForFile, fullFileSize,
                    It.IsAny<IList<FilePart>>()),
                Times.Once);
        }

        [TestMethod]
        public async Task UploadsWhenThereIsExistingFileWithDifferentHash()
        {
            // arrange
            var fileName = "foo.txt";
            string filePath = Path.Combine(Path.GetTempPath(), fileName);
            File.WriteAllText(filePath, "123");
            long fullFileSize = new FileInfo(filePath).Length;
            var hashForFile = new MD5FileHasher().CalculateHashForFile(filePath);

            int resourceId = 1;
            var checkFileResult = new CheckFileResult
            {
                StatusCode = HttpStatusCode.NoContent,
                LastModified = DateTimeOffset.UtcNow,
                FileNameOnServer = fileName,
                HashForFileOnServer = "DifferentHash"
            };

            // Have CheckFileAsync return NoContent with a hash
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

            // Crete Update 
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
                        It.IsAny<Stream>(), It.IsAny<FilePart>(), fileName, fullFileSize, countOfFileParts))
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
            await this.classUnderTest.UploadFileAsync(resourceId, filePath, this.cancellationToken);

            // assert
            mockFileService.Verify(
                service => service.CheckFileAsync(1),
                Times.Once);

            mockFileService.Verify(
                service => service.CreateNewUploadSessionAsync(resourceId),
                Times.Once);

            mockFileService.Verify(
                service => service.UploadStreamAsync(resourceId, sessionId,
                    It.IsAny<Stream>(), It.IsAny<FilePart>(), fileName, fullFileSize, countOfFileParts),
                Times.Exactly(countOfFileParts));

            mockFileService.Verify(
                service => service.CommitAsync(resourceId, sessionId, fileName, hashForFile, fullFileSize,
                    It.IsAny<IList<FilePart>>()),
                Times.Once);
        }

        [TestMethod]
        public async Task DoesNotUploadWhenThereIsExistingFileWithSameHash()
        {
            // arrange
            var fileName = "foo.txt";
            string filePath = Path.Combine(Path.GetTempPath(), fileName);
            File.WriteAllText(filePath, "123");
            long fullFileSize = new FileInfo(filePath).Length;
            var hashForFile = new MD5FileHasher().CalculateHashForFile(filePath);

            int resourceId = 1;
            var checkFileResult = new CheckFileResult
            {
                StatusCode = HttpStatusCode.NoContent,
                LastModified = DateTimeOffset.UtcNow,
                FileNameOnServer = fileName,
                HashForFileOnServer = hashForFile
            };

            // Have CheckFileAsync return NoContent with a hash
            mockFileService.Setup(
                    service => service.CheckFileAsync(resourceId))
                .ReturnsAsync(checkFileResult);

            // act
            await this.classUnderTest.UploadFileAsync(resourceId, filePath, this.cancellationToken);

            // assert
            mockFileService.Verify(
                service => service.CheckFileAsync(1),
                Times.Once);

            mockFileService.Verify(
                service => service.SetUploadedAsync(1),
                Times.Once);

            mockFileService.Verify(
                service => service.CreateNewUploadSessionAsync(resourceId),
                Times.Never);

            mockFileService.Verify(
                service => service.UploadStreamAsync(resourceId, It.IsAny<Guid>(),
                    It.IsAny<Stream>(), It.IsAny<FilePart>(), fileName, fullFileSize, It.IsAny<int>()),
                Times.Never);

            mockFileService.Verify(
                service => service.CommitAsync(resourceId, It.IsAny<Guid>(), fileName, hashForFile, fullFileSize,
                    It.IsAny<IList<FilePart>>()),
                Times.Never);
        }
    }
}
