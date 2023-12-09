using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommandLine;

namespace rpaextract;

/// <summary>
///     rpaextract - Copyright (c) 2017-2023 Fabian Creutz.
///     An application for listing/extracting content from Ren'py archives.
/// </summary>
internal sealed class Program {
    /// <summary>
    ///     Defines the entry point of the application.
    /// </summary>
    /// <param name="args">The command-line arguments passed to the application.</param>
    /// <returns>The exit code for the current process.</returns>
    private static Task<int> Main(string[] args) => Parser.Default.ParseArguments<Options>(args).MapResult(RunOptions, _ => Task.FromResult(1));

    /// <summary>
    ///     Runs the program with the parsed command-line arguments.
    /// </summary>
    /// <param name="options">The parsed command-line arguments as an instance of <see cref="Options" />.</param>
    /// <returns>The exit code for the current process.</returns>
    private static async Task<int> RunOptions(Options options) {
        CancellationTokenSource source = new();
        
        // Check if archive operation has been specified.
        if (options is { ListFiles: false, ExtractFiles: false }) {
            if (!options.QuietMode)
                await Console.Error.WriteLineAsync("(Error) No archive operation specified. Use -l to list all files in the archive or -x to extract all files.");
            return 4;
        }
        
        var archivePath = options.Path;
        if (archivePath is null || !File.Exists(archivePath)) {
            if (!options.QuietMode)
                await Console.Error.WriteLineAsync("(Error) Archive not found.");
            return 2;
        }

        FileInfo fi = new(archivePath);
        Archive archive;
        try {
            if(!options.QuietMode)
                Console.WriteLine("Loading archive structure...");
            archive = await Archive.LoadAsync(fi, source.Token);
        } catch (Exception ex) {
            if (!options.QuietMode)
                await Console.Error.WriteLineAsync($"(Error) Failed to open archive: {ex}");
            return 3;
        }

        // Check if files should be listed or extracted.
        if (options.ListFiles) {
            Array.ForEach(archive.GetFiles().ToArray(), Console.WriteLine);
            return 0;
        }

        if (options.ExtractFiles) {
            // Create output directory at archive location.
            if(options.Verbose)
                Console.WriteLine("Creating output directory...");
            var directoryName = fi.DirectoryName ?? throw new ArgumentException("Failed to retrieve directory name for the specified file.");
            var outputPath = string.IsNullOrWhiteSpace(options.OutputDirectory) ? Path.Combine(directoryName, $"rpaextract_{Path.GetFileNameWithoutExtension(fi.Name)}") : Path.GetFullPath(options.OutputDirectory);
            try {
                if (!Directory.Exists(outputPath))
                    Directory.CreateDirectory(outputPath);
            } catch (Exception ex) {
                if (!options.QuietMode)
                    Console.WriteLine($"(Error) Couldn't create output directory: {ex}");
                return ex.HResult;
            }

            // Iterate through every file index.
            if(!options.QuietMode)
                Console.WriteLine($"Extracing files to {outputPath}");
            foreach (ArchiveIndex ind in archive.EnumerateIndices()) {
                // Read file data from index.
                ReadOnlyMemory<byte> data = await archive.ReadAsync(ind, source.Token);
                // Combine output directory with internal archive path.
                var path = Path.Combine(outputPath, ind.FilePath);
                var info = new FileInfo(path);
                if (!Directory.Exists(path))
                    Directory.CreateDirectory(info.DirectoryName ?? throw new ArgumentException("Cannot get directory name."));
                // Write data to disk.
                await using FileStream fs = new(path, FileMode.CreateNew, FileAccess.Write, FileShare.Read);
                await fs.WriteAsync(data, source.Token);
            }

            if (!options.QuietMode)
                Console.WriteLine("Done.");
        } else {
            if (!options.QuietMode)
                Console.WriteLine("Unknown option.");
        }

        return 0;
    }
}
