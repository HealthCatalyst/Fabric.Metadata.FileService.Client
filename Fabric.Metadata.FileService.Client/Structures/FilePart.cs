namespace Fabric.Metadata.FileService.Client.Structures
{
    public class FilePart
    {
        /// <summary>
        /// Gets or sets id in the list
        /// </summary>
        public int Id { get; set; }

        /// <summary>
        /// Gets or sets Offset
        /// </summary>
        public int Offset { get; set; }

        /// <summary>
        /// Gets or sets Size
        /// </summary>
        public int Size { get; set; }

        /// <summary>
        /// Gets or sets Hash
        /// </summary>
        public string Hash { get; set; }

        public string FullPath { get; set; }
    }
}