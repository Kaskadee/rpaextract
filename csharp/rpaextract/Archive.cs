/*
 * Parts of this class were ported from https://github.com/Shizmob/rpatool/ (written in Python) licensed under the "Do What The Fuck You Want To Public License" (WTFPL)
 * https://github.com/Shizmob/rpatool/blob/d0ffa7a/LICENSE
 * - or if unavailable -
 * http://www.wtfpl.net/txt/copying/
 */

using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using JetBrains.Annotations;
using rpaextract.Custom;
using rpaextract.Extensions;
using SharpCompress.Compressors;
using SharpCompress.Compressors.Deflate;
using sharppickle;

namespace rpaextract; 

/// <summary>
///     Provides class to parse and extract Ren'py archives.
/// </summary>
internal sealed class Archive : IDisposable, IAsyncDisposable {
    /// <summary>
    ///     Gets the version of the current Ren'py archive.
    /// </summary>
    public ArchiveVersion Version { get; }

    private readonly ArchiveIndex[]? indices;
    private readonly Stream stream;

    /// <summary>
    ///     Initializes a new instance of the <see cref="Archive"/> class.
    /// </summary>
    /// <param name="stream">The open stream to read data from.</param>
    /// <param name="version">The version of the archive.</param>
    /// <param name="indices">The file indices of the archive.</param>
    private Archive(Stream stream, ArchiveVersion version, IEnumerable<ArchiveIndex>? indices) {
        this.stream = stream ?? throw new ArgumentNullException(nameof(stream));
        this.Version = version;
        if (this.IsSupported()) 
            this.indices = indices?.ToArray() ?? null;
    }

    /// <summary>
    ///     Returns true if the loaded archive is valid and supported.
    /// </summary>
    public bool IsSupported() => this.Version is ArchiveVersion.RPA2 or ArchiveVersion.RPA3 or ArchiveVersion.RPA32 or ArchiveVersion.RPA4;

    /// <summary>
    ///     Gets the list of files in the archive.
    /// </summary>
    public IEnumerable<string> GetFiles() {
        // Check if current archive is valid.
        return !this.IsSupported() || this.indices is null ? throw new NotSupportedException("The archive is not valid or unsupported.") : this.indices.Select(x => x.FilePath).OrderBy(x => x);
    }

    /// <summary>
    ///     Enumerates the archive indices in the archive.
    /// </summary>
    /// <returns>The enumeration of all archive indices as a <seealso cref="ArchiveIndex"/>.</returns>
    public IEnumerable<ArchiveIndex> EnumerateIndices() => this.indices ?? throw new NotSupportedException("The archive is not valid or unsupported.");

    /// <summary>
    ///     Reads the specified file from the archive.
    /// </summary>
    /// <param name="index">The archive index of the file to read from.</param>
    /// <param name="token">The <seealso cref="CancellationToken"/> to cancel the task.</param>
    /// <returns>The contents of the file as a byte-array</returns>
    [PublicAPI]
    public async Task<byte[]> ReadAsync(ArchiveIndex index, CancellationToken token = default) {
        // Check if cancellation is already requested.
        token.ThrowIfCancellationRequested();
        // Validate arguments.
        if(index == null)
            throw new ArgumentNullException(nameof(index));
        if(!this.IsSupported() || this.indices is null)
            throw new NotSupportedException("The archive is not valid or unsupported.");
        if(!this.indices.Contains(index))
            throw new FileNotFoundException("The specified index is not located in the archive.");

        // Seek to file offset.
        this.stream.Seek(index.Offset, SeekOrigin.Begin);
        // Read file content to memory.
        var length = index.Length - index.Prefix.Length;
        var buffer = ArrayPool<byte>.Shared.Rent(length);
        try {
            var bytesRead = await this.stream.ReadAsync(buffer.AsMemory(0, length), token);
            if (bytesRead != length)
                throw new InvalidDataException("Less data read than expected.");
            var data = new byte[index.Length + index.Prefix.Length];
            Buffer.BlockCopy(index.Prefix, 0, data, 0, index.Prefix.Length);
            Buffer.BlockCopy(buffer, 0, data, index.Prefix.Length, data.Length);
            return data;
        } finally {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    /// <summary>
    ///     Loads the Ren'py archive from the specified file asynchronously.
    /// </summary>
    /// <param name="fi">The <see cref="FileInfo"/> of the Ren'py archive to load.</param>
    /// <param name="token">The <seealso cref="CancellationToken"/> to cancel the task.</param>
    /// <returns>The parsed Ren'py archive as an instance of <see cref="Archive"/>.</returns>
    [PublicAPI]
    public static async Task<Archive> LoadAsync(FileInfo fi, CancellationToken token = default) {
        // Validate arguments and file.
        if (fi == null)
            throw new ArgumentNullException(nameof(fi));
        if (!fi.Exists || fi.Attributes.HasFlag(FileAttributes.Directory))
            throw new FileNotFoundException("The specified archive couldn't be found!", fi.FullName);
        if (fi.Length < 51)
            throw new ArgumentOutOfRangeException(nameof(fi), "The file is too small to be a valid archive!");
        // Try to open file in read-only mode.
        await using FileStream fs = fi.Open(FileMode.Open, FileAccess.Read, FileShare.Read);
        // Validate archive version.
        ArchiveVersion version = await GetArchiveVersionAsync(fs, token);
        if (version is ArchiveVersion.Unknown)
            return new(fs, ArchiveVersion.Unknown, null);
        // Parse archive header.
        fs.Seek(0, SeekOrigin.Begin);
        var header = await fs.ReadLineAsync(token);
        var parts = header.Split((char)0x20);
        // Seek to the hexadecimal offset and read archive structure.
        var offset = Convert.ToInt32(parts[1], 16);
        var deobfuscationKey = CalculateDeobfuscationKey(version, parts);
        fs.Seek(offset, SeekOrigin.Begin);
        await using var stream = new ZlibStream(fs, CompressionMode.Decompress);
        await using var parser = new PickleReader(await stream.ReadToEndAsync(token));
        Encoding enc = parser.Encoding ?? Encoding.UTF8;
        // Deserialize pickle data and parse the data as archive indices.
        var deserialized = parser.Unpickle();
        Dictionary<object, object?> rawDict = deserialized.First() as Dictionary<object, object?> ?? throw new InvalidDataException("Failed to get dictionary of archive indices!");
        IEnumerable<ArchiveIndex> indices = rawDict.ToDictionary(pair => (pair.Key as string)!, pair => pair.Value as List<object?>).Select(pair => {
            pair.Deconstruct(out var key, out List<object?>? value);
            if(value is null)
                throw new InvalidDataException("Value must not be null!");
            var (item1, item2, item3) = value.First() as Tuple<object?, object?, object?> ?? throw new InvalidDataException("Failed to retrieve archive index data from deserialized dictionary.");
            var indexOffset = Convert.ToInt64(item1);
            var length = Convert.ToInt32(item2);
            var prefix = enc.GetBytes(item3 as string ?? throw new InvalidDataException("Prefix not saved as string!"));
            return new ArchiveIndex(key, indexOffset ^ deobfuscationKey, length ^ deobfuscationKey, prefix);
        });

        return new(fi.Open(FileMode.Open, FileAccess.Read, FileShare.Read), version, indices);
    }

    /// <summary>
    ///     Gets the archive version from the specified stream.
    /// </summary>
    /// <param name="stream">The <see cref="Stream"/> to read the archive version from.</param>
    /// <param name="token">The <seealso cref="CancellationToken"/> to cancel the task.</param>
    /// <returns>The archive version.</returns>
    private static async Task<ArchiveVersion> GetArchiveVersionAsync(Stream stream, CancellationToken token = default) {
        token.ThrowIfCancellationRequested();
        stream.Seek(0, SeekOrigin.Begin);
        // Check for unoffical custom archives.
        IMemoryOwner<byte> owner = MemoryPool<byte>.Shared.Rent(6);
        try {
            Memory<byte> headerBuffer = owner.Memory;
            await stream.ReadAsync(headerBuffer[..6], token);
            if (headerBuffer.Span.SequenceEqual(YVANeusEX.SupportedHeader.AsSpan()))
                return ArchiveVersion.YVANeusEX;
        } finally {
            owner.Dispose();
        }
        // Read file header to determine offical (or modified) archive version.
        var header = (await stream.ReadLineAsync(token)).Split(' ').First().ToUpperInvariant();
        return header switch {
            "RPA-4.0" => ArchiveVersion.RPA4,
            "RPA-3.2" => ArchiveVersion.RPA32,
            "RPA-3.0" => ArchiveVersion.RPA3,
            "RPA-2.0" => ArchiveVersion.RPA2,
            var _ => ArchiveVersion.Unknown,
        };
    }

    /// <summary>
    ///     Calculates the deobfuscation key for the specified archive header.
    /// </summary>
    /// <param name="version">The ren'py archive version.</param>
    /// <param name="headerParts">The archive header parts.</param>
    /// <returns>The calculated deobfuscation key.</returns>
    private static int CalculateDeobfuscationKey(ArchiveVersion version, IReadOnlyList<string> headerParts) {
        return version switch {
            ArchiveVersion.RPA32 => Convert.ToInt32(headerParts[3], 16),
            ArchiveVersion.RPA3 => Convert.ToInt32(headerParts[2], 16),
            ArchiveVersion.RPA4 => Convert.ToInt32(headerParts[2], 16),
            var _ => 0
        };
    }

    /// <summary>
    ///     Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
    /// </summary>
    public void Dispose() => this.stream.Dispose();

    /// <summary>
    ///     Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources asynchronously.
    /// </summary>
    public ValueTask DisposeAsync() => this.stream.DisposeAsync();
}