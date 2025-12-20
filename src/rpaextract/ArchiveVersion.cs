// rpaextract - ArchiveVersion.cs
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
///     Provides an enumeration of all possible Ren'py archive versions that are known in this version.
/// </summary>
public enum ArchiveVersion {
    /// <summary>
    ///     Unknown version.
    /// </summary>
    Unknown,

    /// <summary>
    ///     Initial ren'py archive version. Not supported.
    /// </summary>
    RPI = 1,

    /// <summary>
    ///     RPA-2.0 archive.
    /// </summary>
    RPA2 = 2,

    /// <summary>
    ///     RPA-3.0 archive.
    /// </summary>
    RPA3 = 3,

    /// <summary>
    ///     Unofficial RPA-3.2 archive.
    /// </summary>
    RPA32 = 4,

    /// <summary>
    ///     Unofficial RPA-4.0 archive.
    /// </summary>
    RPA4 = 5
}
