using CommandLine;

namespace rpaextract;

/// <summary>
///     Provides a model of configuration options which can be controlled using command-line arguments.
/// </summary>
internal sealed class Options {
    /// <summary>
    ///     Gets or sets the path of the RPA archive to extract.
    /// </summary>
    [Option('f', "archive", Required = true, HelpText = "Sets the path to the RPA archive to extract.")]
    public string? Path { get; set; }

    /// <summary>
    ///     Gets or sets a value indicating whether list all files or not.
    /// </summary>
    [Option('l', "list", HelpText = "Prints the path and name of all files in the archive to the standard output.", SetName = "List")]
    public bool ListFiles { get; set; }

    /// <summary>
    ///     Gets or sets a value indicating whether to extract all files or not.
    /// </summary>
    [Option('x', "extract", HelpText = "Extracts all files in the archive to the disk.", SetName = "Extract")]
    public bool ExtractFiles { get; set; }

    /// <summary>
    ///     Gets or sets the output directory.
    /// </summary>
    [Option('o', "output", HelpText = "Sets the directory to extract the files to (only works with -x).", SetName = "Extract")]
    public string? OutputDirectory { get; set; }

    /// <summary>
    ///     Gets or sets a value indicating whether console output should be suppressed or not.
    /// </summary>
    /// <value><c>true</c> if output will be suppressed; otherwise, <c>false</c>.</value>
    [Option('q', "quiet", HelpText = "Suppresses any output to the standard output.")]
    public bool QuietMode { get; set; }
}