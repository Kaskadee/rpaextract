// rpaextract - ArchiveIndex.cs
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

namespace rpaextract;

/// <summary>
///     Represents an archive entry deserialized by an implementation of the <see cref="API.ArchiveReader" /> interface.
/// </summary>
public sealed class ArchiveIndex {
    /// <summary>
    ///     Gets the internal archive path of the file.
    /// </summary>
    public string FilePath { get; }

    /// <summary>
    ///     Gets the offset of the beginning of the file.
    /// </summary>
    public long Offset { get; }

    /// <summary>
    ///     Gets the checksum of the file.
    /// </summary>
    public uint Checksum { get; init; }

    /// <summary>
    ///     Gets the length of the file in bytes.
    /// </summary>
    public int Length { get; }

    /// <summary>
    ///     Gets the prefix of this file. This seems optional and is appended to the beginning of the file data.
    /// </summary>
    public ReadOnlyMemory<byte> Prefix { get; }

    /// <summary>
    ///     Initializes a new instance of the <see cref="ArchiveIndex" /> class.
    /// </summary>
    /// <param name="path">The internal file path in the archive.</param>
    /// <param name="offset">The offset of the beginning of the file in the archive.</param>
    /// <param name="length">The length of the file in bytes.</param>
    /// <param name="prefix">The prefix data of this file.</param>
    public ArchiveIndex(string path, long offset, int length, ReadOnlyMemory<byte> prefix) {
        this.FilePath = path;
        this.Offset = offset;
        this.Length = length;
        this.Prefix = prefix;
    }
}
