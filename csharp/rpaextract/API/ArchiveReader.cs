using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace rpaextract.API;

/// <summary>
/// Provides an abstract class to implement custom Ren'py archive readers to read and extract files from them.
/// </summary>
public abstract class ArchiveReader : IDisposable {
    /// <summary>
    /// Gets the information of the loaded file as an instance of the <see cref="FileInfo"/> class.
    /// </summary>
    protected FileInfo File { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="ArchiveReader"/> class.
    /// </summary>
    /// <param name="file">The information of the file to load as an archive as an instance of <see cref="FileInfo"/> class.</param>
    protected ArchiveReader(FileInfo file) {
        if (!file.Exists || file.Attributes.HasFlag(FileAttributes.Directory))
            throw new FileNotFoundException("The specified archive file was not found.", file.FullName);
        this.File = file;
    }

    /// <summary>
    /// Gets a value indicating whether the loaded file is supported and can be read by this implementation of the <see cref="ArchiveReader"/> interface.
    /// </summary>
    /// <returns><c>true</c>, if the archive is supported; otherwise <c>false</c>.</returns>
    public abstract bool IsSupported();
    /// <summary>
    /// Gets the file name list of the archive as an <see cref="IEnumerable{T}"/>.
    /// </summary>
    /// <returns>The file index list of the loaded archive.</returns>
    public abstract IEnumerable<string> GetFiles();
    /// <summary>
    /// Gets the archive entries as an <see cref="IEnumerable{T}"/>.
    /// </summary>
    /// <returns>The archive entry index list of the loaded archive.</returns>
    public abstract IEnumerable<ArchiveIndex> EnumerateIndices();
    /// <summary>
    /// Loads the archive content from the file specified in the constructor.
    /// </summary>
    /// <param name="token">The <seealso cref="CancellationToken"/> to cancel the asynchronous task.</param>
    public abstract Task LoadAsync(CancellationToken token = default);
    /// <summary>
    /// Reads the content of the specified <see cref="ArchiveIndex"/> from the loaded archive.
    /// </summary>
    /// <param name="index">The <see cref="ArchiveIndex"/> to read from the archive.</param>
    /// <param name="token">The <seealso cref="CancellationToken"/> to cancel the asynchronous task.</param>
    /// <returns>The contents of the file as a <see cref="ReadOnlyMemory{T}"/> of <see cref="byte"/>.</returns>
    public abstract Task<ReadOnlyMemory<byte>> ReadAsync(ArchiveIndex index, CancellationToken token = default);
    /// <summary>
    /// Gets the archive version from the loaded file.
    /// </summary>
    /// <param name="token">The <seealso cref="CancellationToken"/> to cancel the task.</param>
    /// <returns>The archive version as an value of <see cref="ArchiveVersion"/>.</returns>
    public abstract ValueTask<ArchiveVersion> GetArchiveVersionAsync(CancellationToken token = default);
    /// <summary>
    /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
    /// </summary>
    public abstract void Dispose();
}
