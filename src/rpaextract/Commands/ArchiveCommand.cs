// rpaextract - ArchiveCommand.cs
// Copyright (C) 2023 Fabian Creutz.
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

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using JetBrains.Annotations;
using rpaextract.Logging;
using Spectre.Console;
using Spectre.Console.Cli;

namespace rpaextract.Commands;

/// <summary>
/// Provides the default <see cref="AsyncCommand{T}"/> for the program.
/// </summary>
internal sealed class ArchiveCommand : AsyncCommand<ArchiveCommand.Settings> {
    /// <summary>
    /// Provides data class containing the setting values for the <see cref="ArchiveCommand"/>.
    /// </summary>
    public sealed class Settings : CommandSettings {
        /// <summary>
        ///     Gets or sets the path of the RPA archive to extract.
        /// </summary>
        [Description("The path to the Ren'py (.rpa) archive.")]
        [CommandOption("-f|--archive")]
        public string? Path { get; [UsedImplicitly] init; }

        /// <summary>
        ///     Gets or sets a value indicating whether list all files or not.
        /// </summary>
        [Description("Lists all files in the archive by printing the path and name to the standard output. Mutually exclusive with '-x'.")]
        [CommandOption("-l|--list")]
        public bool ListFiles { get; [UsedImplicitly] init; }

        /// <summary>
        ///     Gets or sets a value indicating whether to extract all files or not.
        /// </summary>
        [Description("Extracts all files from the archive to the disk. Mutually exclusive with '-l'.")]
        [CommandOption("-x|--extract")]
        public bool ExtractFiles { get; [UsedImplicitly] init; }

        /// <summary>
        ///     Gets or sets the output directory.
        /// </summary>
        [Description("The output directory to extract files to. Only works with '-x'.")]
        [CommandOption("-o|--output")]
        public string? OutputDirectory { get; [UsedImplicitly] init; }

        /// <summary>
        ///     Gets or sets a value indicating whether console output should be suppressed or not.
        /// </summary>
        /// <value><c>true</c> if output will be suppressed; otherwise, <c>false</c>.</value>
        [Description("Suppresses any output to the standard output. Mutually exclusive with '-v'.")]
        [CommandOption("-q|--quiet")]
        public bool QuietMode { get; [UsedImplicitly] init; }
    
        /// <summary>
        ///     Gets or sets a value indicating whether detailed information should be printed to the console.
        /// </summary>
        [Description("Prints detailed information about the current operation of the program. Mutually exclusive with '-q'.")]
        [CommandOption("-v|--verbose")]
        public bool Verbose { get; [UsedImplicitly] init; }

        /// <summary>
        /// Validates the current <see cref="Settings"/> instance.
        /// </summary>
        /// <returns>Returns a ValidationResult indicating whether the operation parameters are valid or not.</returns>
        public override ValidationResult Validate() {
            if (!this.ListFiles && !this.ExtractFiles)
                return ValidationResult.Error("No archive operation specified. Use -l to list all files in the archive or -x to extract all files.");
            if(this.QuietMode && this.Verbose)
                return ValidationResult.Error("Quiet mode (-q) and verbose mode (-v) are mutually exclusive.");
            if(this.ListFiles && this.ExtractFiles)
                return ValidationResult.Error("List mode (-l) and extract mode (-x) are mutually exclusive.");
            if(this.Path is null || !File.Exists(this.Path))
                return ValidationResult.Error("Archive not found.");
            return base.Validate();
        }
    }

    private readonly CancellationTokenSource cancellationTokenSource = new();

    /// <summary>
    /// Executes the default command of the program.
    /// </summary>
    /// <param name="context">The <see cref="CommandContext"/> to execute this command in.</param>
    /// <param name="settings">The parsed command-line settings as a <see cref="Settings"/> instance.</param>
    /// <returns>The exit code of the application</returns>
    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings) {
        // Set logging mode based on the current settings.
        Log.SetLogMode(settings.Verbose ? LogMode.Verbose : settings.QuietMode ? LogMode.Quiet : LogMode.Normal);
        
        // Load the file information about the archive and read the archive structure.
        FileInfo fi = new(settings.Path!);
        Archive? archive = null;
        try {
            if (settings.QuietMode) {
                archive = await Archive.LoadAsync(fi, this.cancellationTokenSource.Token);
            } else {
                await AnsiConsole.Status().StartAsync("Loading archive structure...", async _ => {
                    archive = await Archive.LoadAsync(fi, this.cancellationTokenSource.Token);
                });
            }
            // Check if file archive has been loaded.
            if (archive is null)
                throw new InvalidOperationException("Archive is null.");
        } catch (Exception ex) {
            Log.Exception("Failed to open archive", ex);
            return -1;
        }
        
        if (settings.ListFiles) {
            // List all files by printing the paths to the standard output.
            Tree tree = this.BuildFileTree(archive);
            AnsiConsole.Write(tree);
        } else if (settings.ExtractFiles) {
            // Create output directory at archive location.
            Log.Verbose("Creating output directory...");
            var directoryName = fi.DirectoryName ?? throw new ArgumentException("Failed to retrieve directory name for the specified file.");
            var outputPath = string.IsNullOrWhiteSpace(settings.OutputDirectory) ? Path.Combine(directoryName, $"rpaextract_{Path.GetFileNameWithoutExtension(fi.Name)}") : Path.GetFullPath(settings.OutputDirectory);
            try {
                if (!Directory.Exists(outputPath))
                    Directory.CreateDirectory(outputPath);
            } catch (Exception ex) {
                Log.Exception("Failed to create output directory: ", ex);
                return ex.HResult;
            }
            
            // Iterate through every file index.
            Log.Info($"Extracting files to {outputPath}");
            await AnsiConsole.Progress().StartAsync(async ctx => {
                ProgressTask task = ctx.AddTask("[green]Extracting files...[/]", maxValue: archive.Length);
                
                foreach (ArchiveIndex ind in archive.EnumerateIndices()) {
                    Log.Verbose($"Extracting {ind.FilePath}");
                    // Read file data from index.
                    ReadOnlyMemory<byte> data = await archive.ReadAsync(ind, this.cancellationTokenSource.Token);
                    // Combine output directory with internal archive path.
                    var path = Path.Combine(outputPath, ind.FilePath);
                    var info = new FileInfo(path);
                    if (!Directory.Exists(path))
                        Directory.CreateDirectory(info.DirectoryName ?? throw new ArgumentException("Cannot get directory name."));
                    // Write data to disk.
                    await using FileStream fs = new(path, FileMode.CreateNew, FileAccess.Write, FileShare.Read);
                    await fs.WriteAsync(data, this.cancellationTokenSource.Token);
                    task.Increment(1);
                }
                
                // Stop the task and mark it as finished.
                task.StopTask();
            });
        }
        
        Log.Info("Done.");
        return 0;
    }

    /// <summary>
    /// Builds a file tree structure from the provided archive.
    /// </summary>
    /// <param name="archive">The archive from which to build the file tree.</param>
    /// <returns>The root of the file tree structure.</returns>
    private Tree BuildFileTree(Archive archive) {
        // Get all file paths from the archive.
        var files = archive.GetFiles().ToArray();
            
        // Build tree using Spectre components.
        Tree root = new(":open_file_folder: /");
        Dictionary<string, IHasTreeNodes> treeNodes = new() { ["/"] = root };

        foreach (var file in files) {
            var segments = file.Split('/', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            StringBuilder pathBuilder = new();
            IHasTreeNodes parentNode = root;
            for (var i = 0; i < segments.Length; i++) {
                var seg = segments[i];
                pathBuilder.Append('/').Append(seg);
                var currentPath = pathBuilder.ToString();
                if (!treeNodes.ContainsKey(currentPath)) {
                    if (i == segments.Length - 1) {
                        treeNodes[currentPath] = parentNode.AddNode($":page_facing_up: {seg}");
                    } else {
                        treeNodes[currentPath] = parentNode.AddNode($":open_file_folder: {seg}");
                    }
                }
                parentNode = treeNodes[currentPath];
            }
        }
        return root;
    }
}
