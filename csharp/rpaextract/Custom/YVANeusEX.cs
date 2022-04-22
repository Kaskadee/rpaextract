namespace rpaextract.Custom;
public sealed class YVANeusEX {
    /// <summary>
    ///     Gets the required header of the archive to be able to parse it with the <see cref="YVANeusEX"/> parser.
    /// </summary>
    public static readonly byte[] SupportedHeader = { 0xC3, 0xAE, 0x45, 0xCC, 0xF0, 0x69 };
}
