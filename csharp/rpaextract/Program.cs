using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommandLine;

namespace rpaextract;

/// <summary>
///     rpaextract - Copyright (c) 2017-2022 Fabian Creutz.
///     An application for listing/extracting content from Ren'py archives.
/// </summary>
internal sealed class Program {
    /// <summary>
    ///     Defines the entry point of the application.
    /// </summary>
    /// <param name="args">The command-line arguments passed to the application.</param>
    /// <returns>The exit code for the current process.</returns>
    private static async Task<int> Main(string[] args) => await Parser.Default.ParseArguments<Options>(args).MapResult(RunOptions, _ => Task.FromResult(1));

    /// <summary>
    ///     Runs the program with the parsed command-line arguments.
    /// </summary>
    /// <param name="options">The parsed command-line arguments as an instance of <see cref="Options"/>.</param>
    /// <returns>The exit code for the current process.</returns>
    private static async Task<int> RunOptions(Options options) {
        CancellationTokenSource source = new();
        var archivePath = options.Path;
        if (archivePath is null || !File.Exists(archivePath)) {
            if (!options.QuietMode)
                await Console.Error.WriteLineAsync("(Error) Archive not found.");
            return 2;
        }

        FileInfo fi = new(archivePath);
        Archive archive;
        try {
            archive = await Archive.LoadAsync(fi, source.Token);
        } catch (Exception ex) {
            if (!options.QuietMode)
                await Console.Error.WriteLineAsync($"(Error) Failed to open archive: {ex.Message}");
            return 3;
        }

        // Check if files should be listed or extracted.
        if (options.ListFiles) {
            Array.ForEach(archive.GetFiles().ToArray(), Console.WriteLine);
            return 0;
        }

        if (options.ExtractFiles) {
            // Create output directory at archive location.
            var directoryName = fi.DirectoryName ?? throw new ArgumentException("Cannot get diretory name.");
            var outputPath = string.IsNullOrWhiteSpace(options.OutputDirectory) ? Path.Combine(directoryName, $"rpaextract_{Path.GetFileNameWithoutExtension(fi.Name)}") : options.OutputDirectory;
            try {
                if (!Directory.Exists(outputPath))
                    Directory.CreateDirectory(outputPath);
            } catch (Exception ex) {
                if (!options.QuietMode)
                    Console.WriteLine($"(Error) Couldn't create output directory: {ex.Message}");
                return ex.HResult;
            }

            // Iterate through every file index.
            foreach (ArchiveIndex ind in archive.EnumerateIndices()) {
                // Read file data from index.
                var data = await archive.ReadAsync(ind, source.Token);
                // Combine output directory with internal archive path.
                var path = Path.Combine(outputPath, ind.FilePath);
                var info = new FileInfo(path);
                if (!Directory.Exists(path))
                    Directory.CreateDirectory(info.DirectoryName ?? throw new ArgumentException("Cannot get directory name."));
                // Write data to disk.
                await File.WriteAllBytesAsync(path, data, source.Token);
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