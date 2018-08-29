namespace Fabric.Metadata.FileService.Client.Interfaces
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Threading.Tasks;
    using Events;
    using FileServiceResults;
    using Structures;

    public interface IFileServiceClient: IDisposable
    {
        event NavigatingEventHandler Navigating;
        event NavigatedEventHandler Navigated;

        /// <summary>
        /// This calls HEAD Files({resourceId})
        /// </summary>
        /// <param name="resourceId"></param>
        /// <returns></returns>
        Task<CheckFileResult> CheckFileAsync(int resourceId);

        /// <summary>
        /// This calls POST Files({resourceId})/UploadSessions
        /// </summary>
        /// <param name="resourceId"></param>
        /// <returns></returns>
        Task<CreateSessionResult> CreateNewUploadSessionAsync(int resourceId);

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
        Task<UploadStreamResult> UploadStreamAsync(int resourceId,
            Guid sessionId,
            Stream stream,
            FilePart filePart,
            string fileName,
            long fullFileSize,
            int filePartsCount,
            int numPartsUploaded);

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
        Task<CommitResult> CommitAsync(int resourceId, Guid sessionId,
            string filename, string fileHash, long fileSize, IList<FilePart> utFileParts);

        /// <summary>
        /// This calls GET Files({resourceId})
        /// </summary>
        /// <param name="resourceId"></param>
        /// <param name="utTempPath"></param>
        /// <returns></returns>
        Task<CheckFileResult> DownloadFileAsync(int resourceId, string utTempPath);

        /// <summary>
        /// This calls DELETE Files({resourceId})/UploadSessions
        /// </summary>
        /// <param name="resourceId"></param>
        /// <returns></returns>
        Task<DeleteSessionResult> DeleteUploadSessionAsync(int resourceId);
    }
}