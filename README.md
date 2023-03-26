# rpaextract

An application for listing/extracting content from Ren'py archives. Written in C# 10 / .NET 6.0
Utilizes [sharppickle](https://git.kaskadee.eu/Kaskadee/sharppickle ) to parse python's pickle format.

## Usage

```text
rpaextract 1.3.2
Copyright © 2017-2023 Fabian Creutz

  -f, --archive    Required. Sets the path to the RPA archive to extract.

  -l, --list       Prints the path and name of all files in the archive to the standard output.

  -x, --extract    Extracts all files in the archive to the disk.

  -o, --output     Sets the directory to extract the files to (only works with -x).

  -q, --quiet      Suppresses any output to the standard output.

  --help           Display this help screen.

  --version        Display version information.
```

## Sample archive

A sample RPA-3.0 ren'py archive can be found in the [sample](https://github.com/Kaskadee/rpaextract/src/branch/master/sample ) directory.
It contains a single .png file to test the functionality of rpaextract.

## Dependencies

- [.NET 6.0](https://dotnet.microsoft.com/en-us/download/dotnet/6.0/runtime )
- [sharppickle](https://github.com/Kaskadee/sharppickle ) (pickle deserialization)
- [CommandLineParser](https://github.com/commandlineparser/commandline ) (command-line argument handling / provided by NuGet)

## License

rpaextract is licensed under the [European Union Public Licence v1.2](https://github.com/Kaskadee/rpaextract/src/branch/master/LICENSE )
