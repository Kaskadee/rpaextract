using System;
using System.IO;
using System.Linq;
using System.Reflection;

namespace rpaextract {
    /// <summary>
    ///     rpaextract - Copyright (c) 2017-2018 Fabian Creutz.
    /// </summary>
    internal sealed class Program {
        /// <summary>
        ///     The beginning of the file header for an RPA-2.0 archive.
        /// </summary>
        internal const string MagicHeaderVersion2 = "RPA-2.0";

        /// <summary>
        ///     The beginning of the file header for an RPA-3.0 archive.
        /// </summary>
        internal const string MagicHeaderVersion3 = "RPA-3.0";

        /// <summary>
        ///     The main entry point for this application.
        /// </summary>
        /// <param name="args">The array of command-line arguments.</param>
        private static int Main(string[] args) {
            // Check for enough arguments.
            if (!args.Any()) {
                Console.WriteLine($"(Info) Syntax: {Path.GetFileName(Assembly.GetEntryAssembly().Location)} <archive>");
                return 1;
            }

            // Should the files be listed or extracted?
            var listFiles = args.Contains("--list") || args.Contains("-l");
            // Get file information about the archive.
            var fi = new FileInfo(args.Last());
            if (!fi.Exists) {
                Console.Error.WriteLine("(Error) Archive not found.");
                return 2;
            }

            // Wrap the file information in an archive class and load archive information.
            var archive = new Archive(fi);
            if (listFiles) {
                Array.ForEach(archive.GetFileList(), Console.WriteLine);
                return 0;
            }

            // Create output directory at archive location.
            var directory = fi.DirectoryName;
            var outputPath = Path.Combine(directory, $"rpaextract_{Path.GetFileNameWithoutExtension(fi.Name)}");
            try {
                if (!Directory.Exists(outputPath))
                    Directory.CreateDirectory(outputPath);
            }
            catch (Exception ex) {
                Console.WriteLine($"(Error) Couldn't create output directory: {ex.Message}");
                return ex.HResult;
            }

            // Iterate through every file index.
            foreach (var ind in archive.Indices) {
                // Read file data from index.
                var data = archive.Read(ind);
                // Combine output directory with internal archive path.
                var path = Path.Combine(outputPath, ind.FilePath);
                var info = new FileInfo(path);
                if (!Directory.Exists(path))
                    Directory.CreateDirectory(info.DirectoryName);
                // Write data to disk.
                File.WriteAllBytes(path, data);
            }

            Console.WriteLine("Done.");
            return 0;
        }
    }
}
