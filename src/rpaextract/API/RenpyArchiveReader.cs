﻿using System;
using System.Buffers;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using rpaextract.Extensions;
using sharppickle;

namespace rpaextract.API;

/// <summary>
///     Provides an implementation of the <see cref="ArchiveReader" /> base class to support reading default Ren'py
///     archives.
/// </summary>
public sealed class RenpyArchiveReader : ArchiveReader {
    private readonly List<ArchiveIndex> indices = [];
    private readonly Stream stream;
    private ArchiveVersion? archiveVersion;
    
    /// <summary>
    ///     Gets the number of files in the archive.
    /// </summary>
    public override int Length => this.indices.Count;
    
    /// <summary>
    ///     Initializes a new instance of the <see cref="RenpyArchiveReader" /> class.
    /// </summary>
    /// <param name="file">The information of the file to load as an archive as an instance of <see cref="FileInfo" /> class.</param>
    public RenpyArchiveReader(FileInfo file) : base(file) => this.stream = file.Open(FileMode.Open, FileAccess.Read, FileShare.Read);

    /// <summary>
    ///     Gets a value indicating whether the loaded file is supported and can be read by this implementation of the
    ///     <see cref="ArchiveReader" /> interface.
    /// </summary>
    /// <returns><c>true</c>, if the archive is supported; otherwise <c>false</c>.</returns>
    public override bool IsSupported() => this.archiveVersion is ArchiveVersion.RPA2 or ArchiveVersion.RPA3 or ArchiveVersion.RPA32 or ArchiveVersion.RPA4;

    /// <summary>
    ///     Gets the file name list of the archive as an <see cref="IEnumerable{T}" />.
    /// </summary>
    /// <returns>The file index list of the loaded archive.</returns>
    public override IEnumerable<string> GetFiles() {
        // Check if current archive is valid.
        return !this.IsSupported() || this.indices.Count == 0 ? throw new NotSupportedException("The archive is not valid or unsupported.") : this.indices.Select(x => x.FilePath).OrderBy(x => x);
    }

    /// <summary>
    ///     Gets the archive entries as an <see cref="IEnumerable{T}" />.
    /// </summary>
    /// <returns>The archive entry index list of the loaded archive.</returns>
    public override IEnumerable<ArchiveIndex> EnumerateIndices() => this.indices ?? throw new NotSupportedException("The archive is not valid or unsupported.");

    /// <summary>
    ///     Loads the archive content from the file specified in the constructor.
    /// </summary>
    /// <param name="token">The <seealso cref="CancellationToken" /> to cancel the asynchronous task.</param>
    public override async Task LoadAsync(CancellationToken token = default) {
        if (this.stream.Length < 51)
            throw new InvalidOperationException("The file is too small to be a valid archive!");
        // Validate archive version.
        this.archiveVersion = await this.GetArchiveVersionAsync(token);
        if (!this.IsSupported())
            return;
        // Parse archive header.
        this.stream.Seek(0, SeekOrigin.Begin);
        var header = await this.stream.ReadLineAsync(token);
        var parts = header.Split((char)0x20);
        // Seek to the hexadecimal offset and read archive structure.
        var offset = long.Parse(parts[1], NumberStyles.HexNumber);
        if (offset <= 0)
            throw new InvalidOperationException($"Invalid offset read from archive header (got: {offset})");
        
        // Calculate the deobfuscation key that is required to decrypt the meta data of the archive contents.
        var deobfuscationKey = CalculateDeobfuscationKey(this.archiveVersion.Value, parts);
        this.stream.Seek(offset, SeekOrigin.Begin);
        await using var zlib = new ZLibStream(this.stream, CompressionMode.Decompress, true);
        await using var parser = new PickleReader(await zlib.ReadToEndAsync(token));
        Encoding enc = parser.Encoding ?? Encoding.UTF8;
        // Deserialize pickle data and parse the data as archive indices.
        var deserialized = parser.Unpickle();
        Dictionary<object, object?> rawDict = deserialized.First() as Dictionary<object, object?> ?? throw new InvalidDataException("Failed to get dictionary of archive indices!");
        IEnumerable<ArchiveIndex> ind = rawDict.ToDictionary(pair => (pair.Key as string)!, pair => pair.Value as List<object?>).Select(pair => {
            pair.Deconstruct(out var key, out List<object?>? value);
            if (value is null)
                throw new InvalidDataException("Value must not be null!");
            var (item1, item2, item3) = value.First() as Tuple<object?, object?, object?> ?? throw new InvalidDataException("Failed to retrieve archive index data from deserialized dictionary.");
            var indexOffset = Convert.ToInt64(item1);
            var length = Convert.ToInt32(item2);
            ReadOnlyMemory<byte> prefix = item3 switch {
                null => ReadOnlyMemory<byte>.Empty,
                string encodedPrefix => enc.GetBytes(encodedPrefix),
                ReadOnlyMemory<byte> prefixBuffer => prefixBuffer,
                var _ => throw new InvalidDataException($"Unsupported type for prefix: {item3.GetType().Name}")
            };
            return new ArchiveIndex(key, indexOffset ^ deobfuscationKey, length ^ deobfuscationKey, prefix);
        });
        this.indices.AddRange(ind);
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
        // Validate arguments.
        ArgumentNullException.ThrowIfNull(index);
        if (!this.IsSupported() || this.indices.Count == 0)
            throw new NotSupportedException("The archive is not valid or unsupported.");
        if (!this.indices.Contains(index))
            throw new FileNotFoundException("The specified index is not located in the archive.");

        // Seek to file offset.
        this.stream.Seek(index.Offset, SeekOrigin.Begin);
        // Read file content to memory.
        var length = index.Length - index.Prefix.Length;
        using IMemoryOwner<byte> owner = MemoryPool<byte>.Shared.Rent(length);
        Memory<byte> buffer = owner.Memory[..length];
        await this.stream.ReadExactlyAsync(buffer, token);
        // Join data prefix and data content together.
        byte[] data = [..index.Prefix.Span, ..buffer.Span];
        return data;
    }

    /// <summary>
    ///     Gets the archive version from the loaded file.
    /// </summary>
    /// <param name="token">The <seealso cref="CancellationToken" /> to cancel the task.</param>
    /// <returns>The archive version as an value of <see cref="ArchiveVersion" />.</returns>
    public override async ValueTask<ArchiveVersion> GetArchiveVersionAsync(CancellationToken token = default) {
        if (this.archiveVersion is not null)
            return this.archiveVersion.Value;
        token.ThrowIfCancellationRequested();
        this.stream.Seek(0, SeekOrigin.Begin);
        // Read file header to determine offical (or modified) archive version.
        var header = (await this.stream.ReadLineAsync(token)).Split(' ').First().ToUpperInvariant();
        return header switch {
            "RPA-4.0" => ArchiveVersion.RPA4,
            "RPA-3.2" => ArchiveVersion.RPA32,
            "RPA-3.0" => ArchiveVersion.RPA3,
            "RPA-2.0" => ArchiveVersion.RPA2,
            var _ => ArchiveVersion.Unknown
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
    public override void Dispose() => this.stream.Dispose();
}
