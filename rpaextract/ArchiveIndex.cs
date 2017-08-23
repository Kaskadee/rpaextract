namespace rpaextract
{
    /// <summary>
    /// Represents an RPA archive file index deserialized by <see cref="Pickle.PickleParser"/>
    /// </summary>
    public class ArchiveIndex
    {
        /// <summary>
        /// Gets the internal path of the file in the archive.
        /// </summary>
        public string FilePath { get; }
        /// <summary>
        /// Gets the offset of the beginning of the file in the archive.
        /// </summary>
        public long Offset { get; }
        /// <summary>
        /// Gets the length of the file in bytes.
        /// </summary>
        public int Length { get; }
        /// <summary>
        /// Gets the prefix of this file. This seems to be optional and is appended to the file data.
        /// </summary>
        public byte[] Prefix { get; }
        /// <summary>
        /// Initializes a new instance of the <see cref="ArchiveIndex"/> class.
        /// </summary>
        /// <param name="path">The internal file path in the archive.</param>
        /// <param name="offset">The offset of the beginning of the file in the archive.</param>
        /// <param name="length">The length of the file in bytes.</param>
        /// <param name="prefix">The prefix data of this file.</param>
        public ArchiveIndex(string path, long offset, int length, byte[] prefix)
        {
            FilePath = path;
            Offset = offset;
            Length = length;
            Prefix = prefix;
        }
    }
}
