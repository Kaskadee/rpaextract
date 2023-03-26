/*
 * Parts of this class were ported from https://github.com/Shizmob/rpatool/ (written in Python) licensed under the "Do What The Fuck You Want To Public License" (WTFPL)
 * https://github.com/Shizmob/rpatool/blob/d0ffa7a/LICENSE
 * - or if unavailable -
 * http://www.wtfpl.net/txt/copying/
 */

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using JetBrains.Annotations;
using rpaextract.API;

namespace rpaextract;

/// <summary>
///     Provides class to parse and extract Ren'py archives.
/// </summary>
internal sealed class Archive : IDisposable {
    private static readonly Type[] loadedArchiveReaders = { typeof(RenpyArchiveReader), typeof(YVANeusEXArchiveReader) };

    /// <summary>
    ///     Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
    /// </summary>
    public void Dispose() => this.archiveReader.Dispose();

    /// <summary>
    ///     Gets the version of the current Ren'py archive.
    /// </summary>
    public ArchiveVersion Version { get; }

    private readonly ArchiveReader archiveReader;

    /// <summary>
    ///     Initializes a new instance of the <see cref="Archive" /> class.
    /// </summary>
    /// <param name="reader">The implementation of the <see cref="archiveReader" /> class which is able to read the archive.</param>
    /// <param name="version">The version of the archive.</param>
    private Archive(ArchiveReader reader, ArchiveVersion version) {
        this.archiveReader = reader ?? throw new ArgumentNullException(nameof(reader));
        this.Version = version;
    }

    /// <summary>
    ///     Returns true if the loaded archive is valid and supported.
    /// </summary>
    public bool IsSupported() => this.archiveReader.IsSupported();

    /// <summary>
    ///     Gets the list of files in the archive.
    /// </summary>
    public IEnumerable<string> GetFiles() => this.archiveReader.GetFiles();

    /// <summary>
    ///     Enumerates the archive indices in the archive.
    /// </summary>
    /// <returns>The enumeration of all archive indices as a <seealso cref="ArchiveIndex" />.</returns>
    public IEnumerable<ArchiveIndex> EnumerateIndices() => this.archiveReader.EnumerateIndices();

    /// <summary>
    ///     Reads the specified file from the archive.
    /// </summary>
    /// <param name="index">The archive index of the file to read from.</param>
    /// <param name="token">The <seealso cref="CancellationToken" /> to cancel the task.</param>
    /// <returns>The contents of the file as a byte-array</returns>
    [PublicAPI]
    public async Task<ReadOnlyMemory<byte>> ReadAsync(ArchiveIndex index, CancellationToken token = default) => await this.archiveReader.ReadAsync(index, token);

    /// <summary>
    ///     Loads the Ren'py archive from the specified file asynchronously.
    /// </summary>
    /// <param name="fi">The <see cref="FileInfo" /> of the Ren'py archive to load.</param>
    /// <param name="token">The <seealso cref="CancellationToken" /> to cancel the task.</param>
    /// <returns>The parsed Ren'py archive as an instance of <see cref="Archive" />.</returns>
    [PublicAPI]
    public static async Task<Archive> LoadAsync(FileInfo fi, CancellationToken token = default) {
        // Validate arguments and file.
        if (fi == null)
            throw new ArgumentNullException(nameof(fi));
        if (!fi.Exists || fi.Attributes.HasFlag(FileAttributes.Directory))
            throw new FileNotFoundException("The specified archive couldn't be found!", fi.FullName);
        if (fi.Length < 51)
            throw new ArgumentOutOfRangeException(nameof(fi), "The file is too small to be a valid archive!");
        foreach (Type archiveReaderType in loadedArchiveReaders) {
            Console.WriteLine($"(Info) Trying to parse archive using {archiveReaderType}...");
            // Try to create new instance of archive reader type.
            if (Activator.CreateInstance(archiveReaderType, fi) is not ArchiveReader reader) {
                Console.WriteLine($"(Error) Failed to create instance of {archiveReaderType}.");
                continue;
            }

            // Check if the archive reader is able to parse the archive.
            await reader.LoadAsync(token);
            if (!reader.IsSupported()) {
                reader.Dispose();
                Console.WriteLine($"(Error) {archiveReaderType.FullName} is not able to parse the specified archive.");
                continue;
            }

            return new(reader, await reader.GetArchiveVersionAsync(token));
        }

        throw new NotSupportedException("No registered reader is able to parse the specified archive.");
    }
}
