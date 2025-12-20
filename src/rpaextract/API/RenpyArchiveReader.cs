// rpaextract - RenpyArchiveReader.cs
// Copyright (C) 2017-2026 Fabian Creutz.
// 
// Licensed under the EUPL, Version 1.2 or – as soon they will be approved by the
// European Commission - subsequent versions of the EUPL (the "Licence");
// 
// You may not use this work except in compliance with the Licence.
// You may obtain a copy of the Licence at:
// 
// https://joinup.ec.europa.eu/software/page/eupl
// 
// Unless required by applicable law or agreed to in writing, software distributed under the Licence is distributed on an "AS IS" basis,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the Licence for the specific language governing permissions and limitations under the Licence.

using System.Globalization;
using System.IO.Compression;
using System.Text;
using CommunityToolkit.HighPerformance.Buffers;
using rpaextract.Extensions;
using sharppickle;

namespace rpaextract.API;

/// <summary>
///     Provides an implementation of the <see cref="ArchiveReader" /> base class to support reading default Ren'py
///     archives.
/// </summary>
public sealed class RenpyArchiveReader : ArchiveReader {
    private readonly List<ArchiveIndex> indices = [];
    private readonly FileStream stream;
    private ArchiveVersion? archiveVersion;
    
    /// <inheritdoc />
    public override int Length => this.indices.Count;
    
    /// <summary>
    ///     Initializes a new instance of the <see cref="RenpyArchiveReader" /> class.
    /// </summary>
    /// <param name="file">The information of the file to load as an archive as an instance of <see cref="FileInfo" /> class.</param>
    public RenpyArchiveReader(FileInfo file) {
        if (!file.Exists || file.Attributes.HasFlag(FileAttributes.Directory))
            throw new FileNotFoundException("The specified archive file was not found.", file.FullName);
        this.stream = file.Open(FileMode.Open, FileAccess.Read, FileShare.Read);
    }

    /// <inheritdoc />
    public override bool IsSupported() => this.archiveVersion is ArchiveVersion.RPA2 or ArchiveVersion.RPA3 or ArchiveVersion.RPA32 or ArchiveVersion.RPA4;

    /// <inheritdoc />
    public override IEnumerable<string> GetFiles() {
        return this.IsSupported() && this.indices.Count is not 0
            ? this.indices.Select(x => x.FilePath).OrderBy(x => x)
            : throw new NotSupportedException("The archive is not valid or unsupported.");
    }

    /// <inheritdoc />
    public override IEnumerable<ArchiveIndex> EnumerateIndices() => this.indices ?? throw new NotSupportedException("The archive is not valid or unsupported.");

    /// <inheritdoc />
    public override async Task LoadAsync(CancellationToken token = default) {
        if (this.stream.Length < 51)
            throw new InvalidOperationException("The file is too small to be a valid archive!");

        // Validate archive version.
        this.archiveVersion = await this.GetArchiveVersionAsync(token);
        if (!this.IsSupported())
            return;

        // Parse archive header.
        this.stream.Seek(0, SeekOrigin.Begin);
        string header = await this.stream.ReadLineAsync(token);
        string[] parts = header.Split((char)0x20);

        // Seek to the hexadecimal offset and read archive structure.
        long offset = long.Parse(parts[1], NumberStyles.HexNumber);
        if (offset <= 0)
            throw new InvalidOperationException($"Invalid offset read from archive header (got: {offset})");
        
        // Calculate the deobfuscation key required to decrypt the metadata of the archive contents.
        int deobfuscationKey = CalculateDeobfuscationKey(this.archiveVersion.Value, parts);
        this.stream.Seek(offset, SeekOrigin.Begin);
        await using var zlib = new ZLibStream(this.stream, CompressionMode.Decompress, true);
        await using var parser = new PickleReader(await zlib.ReadToEndAsync(token));
        Encoding enc = parser.Encoding ?? Encoding.UTF8;

        // Deserialize pickle data and parse the data as archive indices.
        object?[] deserialized = parser.Unpickle();
        Dictionary<object, object?> rawDict = deserialized.First() as Dictionary<object, object?> ?? throw new InvalidDataException("Failed to get dictionary of archive indices!");
        IEnumerable<ArchiveIndex> ind = rawDict.ToDictionary(pair => (pair.Key as string)!, pair => pair.Value as List<object?>).Select(pair => {
            pair.Deconstruct(out string key, out List<object?>? value);
            if (value is null)
                throw new InvalidDataException("Value must not be null!");
            (object? item1, object? item2, object? item3) = value.First() as Tuple<object?, object?, object?> ?? throw new InvalidDataException("Failed to retrieve archive index data from deserialized dictionary.");
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

    /// <inheritdoc />
    public override async Task<ReadOnlyMemory<byte>> ReadAsync(ArchiveIndex index, CancellationToken token = default) {
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
        int length = index.Length - index.Prefix.Length;
        using MemoryOwner<byte> owner = MemoryOwner<byte>.Allocate(length);
        await this.stream.ReadExactlyAsync(owner.Memory, token);

        // Join data prefix and data content together.
        byte[] data = [..index.Prefix.Span, ..owner.Span];
        return data;
    }

    /// <inheritdoc />
    public override async ValueTask<ArchiveVersion> GetArchiveVersionAsync(CancellationToken token = default) {
        if (this.archiveVersion is not null)
            return this.archiveVersion.Value;
        token.ThrowIfCancellationRequested();
        this.stream.Seek(0, SeekOrigin.Begin);

        // Read the file header to determine the official (or modified) archive version.
        string header = (await this.stream.ReadLineAsync(token)).Split(' ').First().ToUpperInvariant();
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
            ArchiveVersion.RPA3 or ArchiveVersion.RPA4 => Convert.ToInt32(headerParts[2], 16),
            var _ => 0
        };
    }

    /// <inheritdoc />
    public override void Dispose() => this.stream.Dispose();

    /// <inheritdoc />
    public override ValueTask DisposeAsync() => this.stream.DisposeAsync();
}
