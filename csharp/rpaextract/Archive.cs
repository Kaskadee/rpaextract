/*
 * Parts of this class were ported from https://github.com/Shizmob/rpatool/ (written in Python) licensed under the "Do What The Fuck You Want To Public License" (WTFPL)
 * https://github.com/Shizmob/rpatool/blob/d0ffa7a/LICENSE
 * - or if unavailable -
 * http://www.wtfpl.net/txt/copying/
 */

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using rpaextract.Extensions;
using SharpCompress.Compressors;
using SharpCompress.Compressors.Deflate;
using sharppickle;

namespace rpaextract {
    /// <summary>
    ///     Provides a wrapper to easily access files in an RPA archive. (Ren'Py Archives)
    /// </summary>
    internal sealed class Archive {
        private readonly FileInfo _info;

        /// <summary>
        ///     Gets the file name of the loaded archive.
        /// </summary>
        public string FileName { get; }

        /// <summary>
        ///     Gets the RPA archive version.
        /// </summary>
        public int Version { get; }

        /// <summary>
        ///     Gets the list of file indices that are contained in the archive.
        /// </summary>
        public ArchiveIndex[] Indices { get; }

        /// <summary>
        ///     Initializes a new instance of the <see cref="Archive" /> class and loads archive information from the specified
        ///     file.
        /// </summary>
        /// <param name="info">The file information of the RPA archive.</param>
        public Archive(FileInfo info) {
            _info = info;
            FileName = info.Name;
            // Determine archive version by reading the file header
            Version = GetArchiveVersion(info);
            // Check if a valid archive has been loaded
            if (!IsValid())
                return;
            // Extract file indices from archive
            Indices = ExtractIndices().ToArray();
        }

        /// <summary>
        ///     Returns true if the loaded archive is valid.
        /// </summary>
        public bool IsValid() => Version > 0 && Version <= 3;

        /// <summary>
        ///     Gets the list of files contained in the archive.
        /// </summary>
        public string[] GetFileList() => IsValid() ? Indices.Select(x => x.FilePath).OrderBy(x => x).ToArray() : null;

        /// <summary>
        ///     Reads the specified file from the archive.
        /// </summary>
        /// <param name="index">The archive index of the file to read from.</param>
        /// <returns>The contents of the file as a byte-array</returns>
        public byte[] Read(ArchiveIndex index) {
            // Check if file exists in loaded indices
            if (index == null || !Indices.Contains(index))
                throw new FileNotFoundException($"{index?.FilePath} couldn't be found in the archive!");
            using var archiveFs = _info.OpenRead();
            using var br = new BinaryReader(archiveFs, Encoding.UTF8);
            // Seek to calculated file offset
            archiveFs.Seek(index.Offset, SeekOrigin.Begin);
            // Read file contents from archive
            var data = br.ReadBytes(index.Length - index.Prefix.Length);
            // Merge prefix and data to one data block.
            var completeData = new byte[index.Prefix.Length + data.Length];
            Buffer.BlockCopy(index.Prefix, 0, completeData, 0, index.Prefix.Length);
            Buffer.BlockCopy(data, 0, completeData, index.Prefix.Length, data.Length);
            // Cleanup unused data.
            Array.Clear(data, 0, data.Length);
            Array.Clear(index.Prefix, 0, index.Prefix.Length);
            return completeData;
        }

        /// <summary>
        ///     Determines the archive version from its file header.
        /// </summary>
        /// <param name="info">The file information about the archive.</param>
        /// <returns>The archive version of the specified archive.</returns>
        /// <exception cref="FileNotFoundException">Thrown if the specified file doesn't exist.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown if the file is too small.</exception>
        /// <exception cref="InvalidDataException">Thrown if the specified file isn't a (supported) RPA archive.</exception>
        private int GetArchiveVersion(FileInfo info) {
            if (!info.Exists || info.Attributes.HasFlag(FileAttributes.Directory))
                throw new FileNotFoundException("The specified archive couldn't be found!", info.FullName);
            if (info.Length < 51)
                throw new ArgumentOutOfRangeException(nameof(info), "The file is too small to be a valid archive!");
            using var fs = info.OpenRead();
            // Read the first seven bytes from the file (this is the archive version)
            var header = fs.ReadLine();
            if (header.StartsWith("RPA-3.0"))
                return 3;
            if (header.StartsWith("RPA-2.0"))
                return 2;
            // If the archive isn't version 2.0/3.0 and it's extension is '.rpi' it is probably a version 1.0 archive
            // TODO Unsupported yet
            // At this point it is probably safe to assume the file isn't an RPA archive.
            throw new InvalidDataException("The specified file isn't a (supported) RPA archive!");
        }

        /// <summary>
        ///     Extracts information about all file indices from the archive.
        /// </summary>
        /// <returns>The list of all file indices found in the archive.</returns>
        private IEnumerable<ArchiveIndex> ExtractIndices() {
            using var fs = _info.Open(FileMode.Open, FileAccess.Read, FileShare.Read);
            // Read header line and split it at whitespace.
            var header = fs.ReadLine();
            var parts = header.Split((char)0x20);
            // Seek to the hexadecimal offset and read archive structure.
            var offset = Convert.ToInt32(parts[1], 16);
            var deobfuscationKey = Version < 3 ? 0 : CalculateDeobfuscationKey(parts);
            fs.Seek(offset, SeekOrigin.Begin);
            using var stream = new ZlibStream(fs, CompressionMode.Decompress);
            using var parser = new PickleReader(stream);
            // Deserialize pickle data and parse the data as archive indices.
            var deserialized = parser.Unpickle();
            var indices = ((Dictionary<object, object>)deserialized[0]).ToDictionary(key => key.Key as string, value => value.Value as ArrayList).Select(pair => {
                var (key, value) = pair;
                var (item1, item2, item3) = value[0] as Tuple<object, object, object> ?? throw new InvalidDataException("Failed to retrieve archive index data from deserialized dictionary.");
                var indexOffset = Convert.ToInt64(item1);
                var length = Convert.ToInt32(item2);
                var prefix = parser.Encoding.GetBytes((string)item3);
                return new ArchiveIndex(key, Version < 3 ? indexOffset : indexOffset ^ deobfuscationKey, Version < 3 ? length : length ^ deobfuscationKey, prefix);
            });

            return indices;
        }

        /// <summary>
        ///     Calculates the deobfuscation key for this archive.
        /// </summary>
        /// <param name="headerParts">The archive header parts.</param>
        /// <returns>The calculated deobfuscation key.</returns>
        private static int CalculateDeobfuscationKey(IEnumerable<string> headerParts) => headerParts.Skip(2).Aggregate(0, (current, value) => current ^ Convert.ToInt32(value, 16));
    }
}
