namespace Fabric.Metadata.FileService.Client.Events
{
    public delegate void NavigatingEventHandler(object sender, NavigatingEventArgs e);
    public delegate void NavigatedEventHandler(object sender, NavigatedEventArgs e);
    public delegate void PartUploadedEventHandler(object sender, PartUploadedEventArgs e);
    public delegate void FileUploadStartedEventHandler(object sender, FileUploadStartedEventArgs e);
    public delegate void CalculatingHashEventHandler(object sender, CalculatingHashEventArgs e);
    public delegate void FileUploadCompletedEventHandler(object sender, FileUploadCompletedEventArgs e);
    public delegate void UploadErrorEventHandler(object sender, UploadErrorEventArgs e);
    public delegate void SessionCreatedEventHandler(object sender, SessionCreatedEventArgs e);
    public delegate void FileCheckedEventHandler(object sender, FileCheckedEventArgs e);
    public delegate void TransientErrorEventHandler(object sender, TransientErrorEventArgs e);
    public delegate void AccessTokenRequestedEventHandler(object sender, AccessTokenRequestedEventArgs e);
    public delegate void NewAccessTokenRequestedEventHandler(object sender, NewAccessTokenRequestedEventArgs e);
    public delegate void CommittingEventHandler(object sender, CommittingEventArgs e);
}
