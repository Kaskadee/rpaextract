/*
 * Parts of this class were ported from https://github.com/Shizmob/rpatool/ (written in Python) licensed under the "Do What The Fuck You Want To Public License" (WTFPL)
 * https://github.com/Shizmob/rpatool/blob/d0ffa7a/LICENSE
 * - or if unavailable -
 * http://www.wtfpl.net/txt/copying/
 */

using System;
using System.Buffers;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using JetBrains.Annotations;
using rpaextract.Extensions;
using SharpCompress.Compressors;
using SharpCompress.Compressors.Deflate;
using sharppickle;

namespace rpaextract {
    /// <summary>
    ///     Provides class to parse and extract Ren'py archives.
    /// </summary>
    internal sealed class Archive : IDisposable, IAsyncDisposable {
        /// <summary>
        ///     Gets the version of the current Ren'py archive.
        /// </summary>
        public ArchiveVersion Version { get; }

        private readonly ArchiveIndex[] _indices;
        private readonly Stream _stream;

        /// <summary>
        ///     Initializes a new instance of the <see cref="Archive"/> class.
        /// </summary>
        /// <param name="stream">The open stream to read data from.</param>
        /// <param name="version">The version of the archive.</param>
        /// <param name="indices">The file indices of the archive.</param>
        private Archive(Stream stream, ArchiveVersion version, IEnumerable<ArchiveIndex> indices) {
            _stream = stream ?? throw new ArgumentNullException(nameof(stream));
            Version = version;
            if (IsSupported())
                _indices = indices.ToArray();
        }

        /// <summary>
        ///     Returns true if the loaded archive is valid and supported.
        /// </summary>
        public bool IsSupported() => Version == ArchiveVersion.RPA2 || Version == ArchiveVersion.RPA3 || Version == ArchiveVersion.RPA32 || Version == ArchiveVersion.RPA4;

        /// <summary>
        ///     Gets the list of files in the archive.
        /// </summary>
        public IEnumerable<string> GetFiles() {
            // Check if current archive is valid.
            if(!IsSupported())
                throw new NotSupportedException("The archive is not valid or unsupported.");
            return _indices.Select(x => x.FilePath).OrderBy(x => x);
        }

        /// <summary>
        ///     Enumerates the archive indices in the archive.
        /// </summary>
        /// <returns>The enumeration of all archive indices as a <seealso cref="ArchiveIndex"/>.</returns>
        public IEnumerable<ArchiveIndex> EnumerateIndices() => _indices;

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
            if(!IsSupported())
                throw new NotSupportedException("The archive is not valid or unsupported.");
            if(!_indices.Contains(index))
                throw new FileNotFoundException("The specified index is not located in the archive.");

            // Seek to file offset.
            _stream.Seek(index.Offset, SeekOrigin.Begin);
            // Read file content to memory.
            var length = index.Length - index.Prefix.Length;
            var buffer = ArrayPool<byte>.Shared.Rent(length);
            try {
                var bytesRead = await _stream.ReadAsync(buffer, 0, length, token);
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
            var fs = fi.Open(FileMode.Open, FileAccess.Read, FileShare.Read);
            // Validate archive version.
            var version = await GetArchiveVersionAsync(fs, token);
            if (version == 0)
                return new Archive(fs, 0, null);
            // Parse archive header.
            fs.Seek(0, SeekOrigin.Begin);
            var header = await fs.ReadLineAsync(token);
            var parts = header.Split((char)0x20);
            // Seek to the hexadecimal offset and read archive structure.
            var offset = Convert.ToInt32(parts[1], 16);
            var deobfuscationKey = CalculateDeobfuscationKey(version, parts);
            fs.Seek(offset, SeekOrigin.Begin);
            await using var stream = new ZlibStream(fs, CompressionMode.Decompress);
            using var parser = new PickleReader(await stream.ReadToEndAsync(token));
            var enc = parser.Encoding;
            // Deserialize pickle data and parse the data as archive indices.
            var deserialized = parser.Unpickle();
            var indices = ((Dictionary<object, object>)deserialized[0]).ToDictionary(key => key.Key as string, value => value.Value as ArrayList).Select(pair => {
                var (key, value) = pair;
                var (item1, item2, item3) = value[0] as Tuple<object, object, object> ?? throw new InvalidDataException("Failed to retrieve archive index data from deserialized dictionary.");
                var indexOffset = Convert.ToInt64(item1);
                var length = Convert.ToInt32(item2);
                var prefix = enc.GetBytes((string)item3);
                return new ArchiveIndex(key, indexOffset ^ deobfuscationKey, length ^ deobfuscationKey, prefix);
            });

            return new Archive(fs, version, indices);
        }

        /// <summary>
        ///     Gets the archive version from the specified stream.
        /// </summary>
        /// <param name="stream">The <see cref="Stream"/> to read the archive version from.</param>
        /// <param name="token">The <seealso cref="CancellationToken"/> to cancel the task.</param>
        /// <returns>The archive version.</returns>
        private static async Task<ArchiveVersion> GetArchiveVersionAsync(Stream stream, CancellationToken token = default) {
            // Seek to beginning of stream.
            token.ThrowIfCancellationRequested();
            stream.Seek(0, SeekOrigin.Begin);
            // Read the first seven bytes from the file (this is the archive version)
            var header = await stream.ReadLineAsync(token);
            if (header.StartsWith("RPA-4.0", StringComparison.OrdinalIgnoreCase))
                return ArchiveVersion.RPA4;
            if (header.StartsWith("RPA-3.2", StringComparison.OrdinalIgnoreCase))
                return ArchiveVersion.RPA32;
            if (header.StartsWith("RPA-3.0", StringComparison.OrdinalIgnoreCase))
                return ArchiveVersion.RPA3;
            if (header.StartsWith("RPA-2.0", StringComparison.OrdinalIgnoreCase))
                return ArchiveVersion.RPA2;
            // TODO If the archive isn't version 2.0/3.0 and it's extension is '.rpi' it is probably a version 1.0 archive
            return ArchiveVersion.Unknown;
        }

        /// <summary>
        ///     Calculates the deobfuscation key for the specified archive header.
        /// </summary>
        /// <param name="version">The ren'py archive version.</param>
        /// <param name="headerParts">The archive header parts.</param>
        /// <returns>The calculated deobfuscation key.</returns>
        private static int CalculateDeobfuscationKey(ArchiveVersion version, IReadOnlyList<string> headerParts) {
            return version switch {
                ArchiveVersion.RPA32 => Convert.ToInt32(headerParts[3]),
                ArchiveVersion.RPA3 => Convert.ToInt32(headerParts[2]),
                ArchiveVersion.RPA4 => Convert.ToInt32(headerParts[2]),
                _ => 0
            };
        }

        /// <summary>
        ///     Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        public void Dispose() => _stream?.Dispose();

        /// <summary>
        ///     Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources asynchronously.
        /// </summary>
        public async ValueTask DisposeAsync() {
            if (_stream != null)
                await _stream.DisposeAsync();
        }
    }
}
