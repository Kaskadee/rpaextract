using CommandLine;
using JetBrains.Annotations;

namespace rpaextract;

/// <summary>
///     Provides a model of configuration options which can be controlled using command-line arguments.
/// </summary>
internal sealed class Options {
    /// <summary>
    ///     Gets or sets the path of the RPA archive to extract.
    /// </summary>
    [Option('f', "archive", Required = true, HelpText = "Sets the path to the RPA archive to extract.")]
    public string? Path { get; [UsedImplicitly] set; }

    /// <summary>
    ///     Gets or sets a value indicating whether list all files or not.
    /// </summary>
    [Option('l', "list", HelpText = "Prints the path and name of all files in the archive to the standard output.", SetName = "List")]
    public bool ListFiles { get; [UsedImplicitly] set; }

    /// <summary>
    ///     Gets or sets a value indicating whether to extract all files or not.
    /// </summary>
    [Option('x', "extract", HelpText = "Extracts all files in the archive to the disk.", SetName = "Extract")]
    public bool ExtractFiles { get; [UsedImplicitly] set; }

    /// <summary>
    ///     Gets or sets the output directory.
    /// </summary>
    [Option('o', "output", HelpText = "Sets the directory to extract the files to (only works with -x).", SetName = "Extract")]
    public string? OutputDirectory { get; [UsedImplicitly] set; }

    /// <summary>
    ///     Gets or sets a value indicating whether console output should be suppressed or not.
    /// </summary>
    /// <value><c>true</c> if output will be suppressed; otherwise, <c>false</c>.</value>
    [Option('q', "quiet", HelpText = "Suppresses any output to the standard output.", SetName = "Logging")]
    public bool QuietMode { get; [UsedImplicitly] set; }
    
    /// <summary>
    ///     Gets or sets a value indicating whether detailed information should be printed to the console.
    /// </summary>
    [Option('v', "verbose", HelpText = "Prints detailed information about the current operation of the program.", SetName = "Logging")]
    public bool Verbose { get; [UsedImplicitly] set; }
}
