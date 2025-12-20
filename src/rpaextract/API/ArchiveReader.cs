// rpaextract - ArchiveReader.cs
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

namespace rpaextract.API;

/// <summary>
///     Provides an abstract class to implement custom Ren'py archive readers to read and extract files from them.
/// </summary>
public abstract class ArchiveReader : IDisposable, IAsyncDisposable {
    /// <summary>
    ///     Gets the number of files in the archive.
    /// </summary>
    public abstract int Length { get; }

    /// <summary>
    ///     Gets a value indicating whether the loaded file is supported and can be read by this implementation of the
    ///     <see cref="ArchiveReader" /> interface.
    /// </summary>
    /// <returns><c>true</c>, if the archive is supported; otherwise <c>false</c>.</returns>
    public abstract bool IsSupported();

    /// <summary>
    ///     Gets the file name list of the archive as an <see cref="IEnumerable{T}" />.
    /// </summary>
    /// <returns>The file index list of the loaded archive.</returns>
    public abstract IEnumerable<string> GetFiles();

    /// <summary>
    ///     Gets the archive entries as an <see cref="IEnumerable{T}" />.
    /// </summary>
    /// <returns>The archive entry index list of the loaded archive.</returns>
    public abstract IEnumerable<ArchiveIndex> EnumerateIndices();

    /// <summary>
    ///     Loads the archive content from the file specified in the constructor.
    /// </summary>
    /// <param name="token">The <seealso cref="CancellationToken" /> to cancel the asynchronous task.</param>
    public abstract Task LoadAsync(CancellationToken token = default);

    /// <summary>
    ///     Reads the content of the specified <see cref="ArchiveIndex" /> from the loaded archive.
    /// </summary>
    /// <param name="index">The <see cref="ArchiveIndex" /> to read from the archive.</param>
    /// <param name="token">The <seealso cref="CancellationToken" /> to cancel the asynchronous task.</param>
    /// <returns>The contents of the file as a <see cref="ReadOnlyMemory{T}" /> of <see cref="byte" />.</returns>
    public abstract Task<ReadOnlyMemory<byte>> ReadAsync(ArchiveIndex index, CancellationToken token = default);

    /// <summary>
    ///     Gets the archive version from the loaded file.
    /// </summary>
    /// <param name="token">The <seealso cref="CancellationToken" /> to cancel the task.</param>
    /// <returns>The archive version as the corresponding <see cref="ArchiveVersion" /> value.</returns>
    public abstract ValueTask<ArchiveVersion> GetArchiveVersionAsync(CancellationToken token = default);
    
    /// <inheritdoc />
    public abstract void Dispose();

    /// <inheritdoc />
    public abstract ValueTask DisposeAsync();
}
