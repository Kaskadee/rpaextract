/*
 * Parts of this class were ported from https://github.com/Shizmob/rpatool/ (written in Python) licensed under the "Do What The Fuck You Want To Public License" (WTFPL)
 * https://github.com/Shizmob/rpatool/blob/d0ffa7a/LICENSE
 * - or if unavailable -
 * http://www.wtfpl.net/txt/copying/
 */

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using rpaextract.Extensions;
using rpaextract.Pickle;
using SharpCompress.Compressors;
using SharpCompress.Compressors.Deflate;

namespace rpaextract {
    /// <summary>
    ///     Provides a wrapper to easily access files in an RPA archive. (Ren'py Archives)
    /// </summary>
    internal sealed class Archive {
        private readonly FileInfo _info;
        private int _deobfuscationKey;

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
        public bool IsValid() {
            return Version > 0 && Version <= 3;
        }

        /// <summary>
        ///     Gets the list of files contained in the archive.
        /// </summary>
        public string[] GetFileList()
        {
            return IsValid() ? Indices.Select(x => x.FilePath).ToArray() : null;
        }

        /// <summary>
        ///     Reads the specified file from the archive.
        /// </summary>
        /// <param name="index">The archive index of the file to read from.</param>
        /// <returns>The contents of the file as a byte-array</returns>
        public byte[] Read(ArchiveIndex index) {
            // Check if file exists in loaded indices
            if (index == null || !Indices.Contains(index))
                throw new FileNotFoundException($"{index?.FilePath} couldn't be found in the archive!");
            using (var archiveFs = _info.OpenRead()) {
                using (var br = new BinaryReader(archiveFs, Encoding.UTF8)) {
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
            }
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
            // Check if the archive exists
            if (!info.Exists || info.Attributes.HasFlag(FileAttributes.Directory))
                throw new FileNotFoundException("The specified archive couldn't be found!", info.FullName);
            // Check if there is a possible valid header
            if (info.Length < 51)
                throw new ArgumentOutOfRangeException(nameof(info), "The file is too small to be a valid archive!");
            // Open the specified file in read-only mode
            using (var fs = info.OpenRead()) {
                // Read the first seven bytes from the file (this is the archive version)
                var header = fs.ReadLine();
                // Determine the archive version
                if (header.StartsWith(Program.MagicHeaderVersion3))
                    return 3;
                if (header.StartsWith(Program.MagicHeaderVersion2))
                    return 2;
                // If the archive isn't version 2.0/3.0 and it's extension is '.rpi' it is probably a version 1.0 archive
                // TODO Unsupported yet
                // At this point it is probably safe to assume the file isn't an RPA archive.
                throw new InvalidDataException("The specified file isn't a (supported) RPA archive!");
            }
        }

        /// <summary>
        ///     Extracts information about all file indicies from the archive.
        /// </summary>
        /// <returns>A list of all file indicies found in the archive.</returns>
        private IEnumerable<ArchiveIndex> ExtractIndices() {
            var parser = new PickleParser();
            // Open archive in read-only mode
            using (var fs = _info.OpenRead()) {
                // Read header line and split it at whitespace.
                var header = fs.ReadLine();
                var splitted = header.Split((char) 0x20);
                // Retrieve hexadecimal offset and convert it to an integer value.
                var offset = Convert.ToInt32(splitted[1], 16);
                // Seek to the determined offset
                fs.Seek(offset, SeekOrigin.Begin);
                // Read the rest of the file (file metadata)
                var data = fs.ReadToEnd();
                // Open data in memory stream and decompress with zlib
                var ms = new MemoryStream(data);
                var stream = new ZlibStream(ms, CompressionMode.Decompress);
                var decompressed = stream.ReadToEnd();
                // Unpickle the decompressed data
                var deserialized = parser.Unpickle(decompressed);
                if (Version != 3) return deserialized;
                // If this is an RPA-3.0 archive additionally calculate the deobfuscation key.
                CalculateDeobfuscationKey(splitted);
                // Deobfuscate archive indicies.
                deserialized = Deobfuscate(deserialized);
                return deserialized;
            }
        }

        /// <summary>
        ///     Deobfuscates the specified archive indices.
        /// </summary>
        /// <param name="indices">The list of obfuscated archive indices.</param>
        private IEnumerable<ArchiveIndex> Deobfuscate(IEnumerable<ArchiveIndex> indices) {
            // Apply deobfuscation key by using binary XOR on offset and length.
            return indices.Select(ind => new ArchiveIndex(ind.FilePath, ind.Offset ^ _deobfuscationKey,
                ind.Length ^ _deobfuscationKey, ind.Prefix));
        }

        /// <summary>
        ///     Calculates the deobfuscation key for this archive.
        /// </summary>
        /// <param name="splittedHeader">The archive header as an splitted array.</param>
        private void CalculateDeobfuscationKey(IEnumerable<string> splittedHeader) {
            // Reset key to zero.
            _deobfuscationKey = 0;
            // Calculate key from archive header.
            foreach (var subkey in splittedHeader.Skip(2))
                _deobfuscationKey ^= Convert.ToInt32(subkey, 16);
        }
    }
}
