using System;
using System.Text;
using rpaextract.Commands;
using Spectre.Console.Cli;

// Run default command using the provided command-line arguments.
Console.OutputEncoding = Encoding.UTF8;
CommandApp<ArchiveCommand> app = new();
return await app.RunAsync(args);