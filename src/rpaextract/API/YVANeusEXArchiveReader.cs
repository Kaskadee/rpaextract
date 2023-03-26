using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using rpaextract.Extensions;

namespace rpaextract.API;

/// <summary>
///     Provides an implementation of the <see cref="ArchiveReader" /> base class to support reading archives with the
///     custom "YVANeusEX" encryption.
/// </summary>
public sealed class YVANeusEXArchiveReader : ArchiveReader {
    /// <summary>
    ///     Gets the required header of the archive to be able to parse it with the <see cref="YVANeusEXArchiveReader" />
    ///     parser.
    /// </summary>
    public static readonly byte[] SupportedHeader = { 0xC3, 0xAE, 0x45, 0xCC, 0xF0, 0x69 };

    private readonly IList<ArchiveIndex> archiveIndices = new List<ArchiveIndex>();

    private readonly Stream stream;
    private ArchiveVersion? archiveVersion;
    private string encryptionKey;
    private bool hasChecksum;
    private bool isCompressed;
    private bool isEncrypted;

    private bool isLoaded;

    /// <summary>
    ///     Initializes a new instance of the <see cref="YVANeusEXArchiveReader" /> class.
    /// </summary>
    /// <param name="file">The information of the file to load as an archive as an instance of <see cref="FileInfo" /> class.</param>
    public YVANeusEXArchiveReader(FileInfo file) : base(file) {
        this.stream = file.Open(FileMode.Open, FileAccess.Read, FileShare.Read);
        this.encryptionKey = string.Empty;
    }

    /// <summary>
    ///     Gets a value indicating whether the loaded file is supported and can be read by this implementation of the
    ///     <see cref="ArchiveReader" /> interface.
    /// </summary>
    /// <returns><c>true</c>, if the archive is supported; otherwise <c>false</c>.</returns>
    public override bool IsSupported() => this.archiveVersion is ArchiveVersion.YVANeusEX;

    /// <summary>
    ///     Gets the file name list of the archive as an <see cref="IEnumerable{T}" />.
    /// </summary>
    /// <returns>The file index list of the loaded archive.</returns>
    public override IEnumerable<string> GetFiles() => !this.IsSupported() || !this.archiveIndices.Any() ? throw new NotSupportedException("The archive is not valid or unsupported.") : this.archiveIndices.Select(x => x.FilePath).OrderBy(x => x);

    /// <summary>
    ///     Gets the archive entries as an <see cref="IEnumerable{T}" />.
    /// </summary>
    /// <returns>The archive entry index list of the loaded archive.</returns>
    public override IEnumerable<ArchiveIndex> EnumerateIndices() => this.archiveIndices ?? throw new NotSupportedException("The archive is not valid or unsupported.");

    /// <summary>
    ///     Loads the archive content from the file specified in the constructor.
    /// </summary>
    /// <param name="token">The <seealso cref="CancellationToken" /> to cancel the asynchronous task.</param>
    public override async Task LoadAsync(CancellationToken token = default) {
        // Validate archive version.
        if (await this.GetArchiveVersionAsync(token) is not ArchiveVersion.YVANeusEX)
            return;
        // Seek to the beginning of the file to start loading the archive.
        this.stream.Seek(0, SeekOrigin.Begin);
        // Read archive meta information from the first line.
        var header = await this.stream.ReadLineAsync(token);
        var mode = (byte)header[6..7].First();
        this.isCompressed = (mode & 1) != 0;
        this.isEncrypted = (mode & 2) != 0;
        this.hasChecksum = (mode & 4) != 0;
        this.encryptionKey = header[7..];
        // Read and decrypt archive file list.
        long dataOffset = 0;
        List<(string fileName, long offset, int size, uint checksum, ReadOnlyMemory<byte> prefix)> result = new();
        try {
            using IMemoryOwner<byte> owner = MemoryPool<byte>.Shared.Rent(4096);
            while (this.stream.Position < this.stream.Length) {
                // Read length of index information as a 16-bit unsigned integer in big endian order.
                Memory<byte> buffer = owner.Memory[..sizeof(ushort)];
                var bytesRead = await this.stream.ReadAsync(buffer, token);
                if (bytesRead != sizeof(ushort))
                    throw new InvalidDataException($"Failed to read enough bytes for {typeof(ushort)}.");
                if (BitConverter.IsLittleEndian)
                    buffer.Span.Reverse();
                var indexLength = BitConverter.ToUInt16(buffer.Span);
                // Check if end of archive has been reached.
                if (indexLength == 0) {
                    dataOffset = this.stream.Position;
                    break;
                }

                // Get file name, size and checksum from index information.
                buffer = owner.Memory[..indexLength];
                bytesRead = await this.stream.ReadAsync(buffer, token);
                if (bytesRead != indexLength)
                    throw new InvalidDataException($"Failed to read enough bytes to read file index information (expected: {indexLength}, got: {bytesRead})");
                // Decrypt file index if archive metainformation is encrypted.
                if (this.isEncrypted)
                    this.Decrypt(buffer.Span, this.encryptionKey);
                var segments = Encoding.UTF8.GetString(buffer.Span).Split('\0');
                var filename = segments[0];
                var size = Convert.ToInt32(segments[1], 16);
                var checksum = Convert.ToUInt32(segments[2], 16);
                result.Add(new(filename, -1, size, checksum, ReadOnlyMemory<byte>.Empty));
            }
        } catch (Exception ex) {
            throw new InvalidDataException("The archive is corrupted.", ex);
        }

        // Iterate through the file list and validate file checksums and determine archive offset.
        foreach ((string fileName, long offset, int size, uint checksum, ReadOnlyMemory<byte> prefix) t in result) {
            (var fileName, var _, var size, var checksum, ReadOnlyMemory<byte> prefix) = t;
            var currentOffset = dataOffset;
            dataOffset += size;
            this.archiveIndices.Add(new(fileName, currentOffset, size, prefix) { Checksum = checksum });
        }

        this.isLoaded = true;
    }

    /// <summary>
    ///     Reads the content of the specified <see cref="ArchiveIndex" /> from the loaded archive.
    /// </summary>
    /// <param name="index">The <see cref="ArchiveIndex" /> to read from the archive.</param>
    /// <param name="token">The <seealso cref="CancellationToken" /> to cancel the asynchronous task.</param>
    /// <returns>The contents of the file as a <see cref="ReadOnlyMemory{T}" /> of <see cref="byte" />.</returns>
    public override async Task<ReadOnlyMemory<byte>> ReadAsync(ArchiveIndex index, CancellationToken token = default) {
        // Check if cancellation is already requested.
        token.ThrowIfCancellationRequested();
        if (!this.isLoaded)
            throw new InvalidOperationException("The archive is not loaded.");
        if (!this.IsSupported() || !this.archiveIndices.Any())
            throw new InvalidOperationException("The archive is not valid or unsupported.");
        if (!this.archiveIndices.Contains(index))
            throw new FileNotFoundException("The specified index is not located in the archive.");
        // Seek to the file offset.
        this.stream.Seek(index.Offset, SeekOrigin.Begin);
        Memory<byte> content = new byte[index.Length];
        var readBytes = await this.stream.ReadAsync(content, token);
        if (readBytes != content.Length)
            throw new InvalidDataException($"Content size mismatch (expected: {content.Length}, got: {readBytes})");
        // Compute and validate checksum if archive metainformation has checksums.
        if (this.hasChecksum) {
            var actualChecksum = this.ComputeChecksum(content.Span);
            if (index.Checksum != actualChecksum)
                throw new InvalidDataException($"Content checksum mismatch (expected: {index.Checksum}, got: {actualChecksum})");
        }

        if (this.isEncrypted)
            this.Decrypt(content.Span, this.encryptionKey);
        return this.isCompressed ? this.Decompress(content) : content;
    }

    /// <summary>
    ///     Gets the archive version from the loaded file.
    /// </summary>
    /// <param name="token">The <seealso cref="CancellationToken" /> to cancel the task.</param>
    /// <returns>The archive version as an value of <see cref="ArchiveVersion" />.</returns>
    public override async ValueTask<ArchiveVersion> GetArchiveVersionAsync(CancellationToken token = default) {
        // Check if cancellation is already requested.
        token.ThrowIfCancellationRequested();
        // If the archive is already loaded return stored version.
        if (this.archiveVersion is not null)
            return this.archiveVersion.Value;
        // Store current position in the stream to seek back later.
        var currentOffset = this.stream.Position;
        this.stream.Seek(0, SeekOrigin.Begin);
        // Read file header and compare it to the supported header.
        using IMemoryOwner<byte> owner = MemoryPool<byte>.Shared.Rent(6);
        Memory<byte> buffer = owner.Memory[..6];
        var readBytes = await this.stream.ReadAsync(buffer, token);
        if (readBytes != buffer.Length)
            throw new InvalidDataException("Failed to read enough bytes to read archive version.");
        this.archiveVersion = buffer.Span.SequenceEqual(SupportedHeader.AsSpan()) ? ArchiveVersion.YVANeusEX : ArchiveVersion.Unknown;
        // Seek back to old offset.
        this.stream.Seek(currentOffset, SeekOrigin.Begin);
        return this.archiveVersion.Value;
    }

    /// <summary>
    ///     Decompresses the specified content using the zlib algorithm.
    /// </summary>
    /// <param name="content">The content to decompress.</param>
    /// <returns>The decompressed content as an instance of <see cref="ReadOnlyMemory{T}" /> of bytes.</returns>
    private unsafe ReadOnlyMemory<byte> Decompress(Memory<byte> content) {
        fixed (byte* buffer = &content.Span[0]) {
            using MemoryStream output = new();
            using UnmanagedMemoryStream unmanagedStream = new(buffer, content.Length);
            using var zlib = new ZLibStream(unmanagedStream, CompressionMode.Decompress);
            zlib.CopyTo(output);
            output.Seek(0, SeekOrigin.Begin);
            return output.ToArray();
        }
    }

    /// <summary>
    ///     Decryptes the specified content using the "YVANeusEX" algorithm with the specified key.
    /// </summary>
    /// <param name="content">The content to decrypt.</param>
    /// <param name="key">The encryption key to decrypt the content with.</param>
    private void Decrypt(Span<byte> content, string key) {
        // Cycle through the encryption key.
        var keyLength = key.Length * 5;
        using IEnumerator<byte> k = Encoding.UTF8.GetBytes(key).Cycle();
        // Read the content and decrypt it using the encryption key.
        if (content.Length >= 2 * keyLength) {
            // Transform prefix and suffix of the data.
            Span<byte> prefix = content[..keyLength];
            Span<byte> suffix = content[^keyLength..];
            for (var i = 0; i < prefix.Length; i++)
                prefix[i] = (byte)(prefix[i] ^ k.Next());
            for (var i = 0; i < suffix.Length; i++)
                suffix[i] = (byte)(suffix[i] ^ k.Next());
        } else {
            // Transform all of the data with the encryption key.
            for (var i = 0; i < content.Length; i++)
                content[i] = (byte)(content[i] ^ k.Next());
        }
    }

    /// <summary>
    ///     Computes the custom Adler-32 checksum of the specified data.
    /// </summary>
    /// <param name="content">The byte content to compute the checksum of.</param>
    /// <returns>The checksum as an <see cref="uint" />.</returns>
    private uint ComputeChecksum(ReadOnlySpan<byte> content) {
        const int mod = 65521;
        uint a = 1;
        uint b = 0;
        foreach (var c in content) {
            a = (a + c) % mod;
            b = (b + a) % mod;
        }

        var adler32 = (b << 16) | a;
        return adler32 & uint.MaxValue;
    }

    /// <summary>
    ///     Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
    /// </summary>
    public override void Dispose() { this.stream.Dispose(); }
}
