using System;
using System.IO;
using System.Linq;
using System.Reflection;

namespace rpaextract
{
    /// <summary>
    /// The main entry class for this application.
    /// </summary>
    internal sealed class Program
    {
        /// <summary>
        /// The beginning of the file header for an RPA-2.0 archive.
        /// </summary>
        internal const string MagicHeaderVersion2 = "RPA-2.0";
        /// <summary>
        /// The beginning of the file header for an RPA-3.0 archive.
        /// </summary>
        internal const string MagicHeaderVersion3 = "RPA-3.0";
        /// <summary>
        /// The main entry point for this application.
        /// </summary>
        /// <param name="args">The array of command-line arguments.</param>
        private static void Main(string[] args)
        {
            // Check for enough arguments.
            if (args.Length == 0)
            {
                // TODO Allow more configuration possiblities
                Console.WriteLine($"(Info) Syntax: {Path.GetFileName(Assembly.GetEntryAssembly().Location)} <archive>");
                Environment.Exit(1);
                return;
            }
            // Get file information about the archive.
            var fi = new FileInfo(args[0]);
            // Create output directory at archives location.
            var directory = fi.DirectoryName;
            var outputPath = Path.Combine(directory, $"rpaextract_{Path.GetFileNameWithoutExtension(fi.Name)}");
            try
            {
                if (!Directory.Exists(outputPath))
                    Directory.CreateDirectory(outputPath);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"(Error) Couldn't create output directory: {ex.Message}");
                Environment.Exit(ex.HResult);
            }
            // Wraps the file information in an archive class and loads archive information.
            var archive = new Archive(fi);
            // TODO Better command-line argument handling.
            if (args.Contains("-l") || args.Contains("--list"))
            {
                // Only list files contained in the archive.
                Array.ForEach(archive.GetFileList(), Console.WriteLine);
            }
            else
            {
                // Iterate through every file index.
                foreach (var ind in archive.Indices)
                {
                    // Read file data from index
                    var data = archive.Read(ind);
                    // Combine output directory with internal archive path
                    var path = Path.Combine(outputPath, ind.FilePath);
                    var info = new FileInfo(path);
                    if (!Directory.Exists(path))
                        Directory.CreateDirectory(info.DirectoryName);
                    // Write data to disk
                    File.WriteAllBytes(path, data);
                }
            }
            Console.WriteLine("Done.");
        }
    }
}
