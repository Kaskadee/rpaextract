namespace rpaextract;

/// <summary>
///     Provides an enumeration of all possible Ren'py archive version that are known in this version.
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
    RPA4 = 5,

    /// <summary>
    ///     Unoffical custom Ren'py archive with encryption (currently only seen in one game).
    /// </summary>
    YVANeusEX = 100
}
