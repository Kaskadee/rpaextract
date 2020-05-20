# rpaextract
An application for listing/extracting content from Ren'py archives. Written in C# 8.0 / .NET Core 3.1
Utilizes [sharppickle](https://git.kaskadee.eu/Kaskadee/sharppickle ) to parse python's pickle format.
## Usage
```
rpaextract 1.1.0
Copyright © 2017-2020 Fabian Creutz

  -f, --archive    Required. Sets the path to the RPA archive to extract.

  -l, --list       Prints the path and name of all files in the archive to the standard output.

  -x, --extract    Extracts all files in the archive to the disk.

  -o, --output     Sets the directory to extract the files to (only works with -x).

  -q, --quiet      Suppresses any output to the standard output.

  --help           Display this help screen.

  --version        Display version information.
```
## Sample archive
A sample RPA-3.0 ren'py archive can be found in the [sample](https://git.kaskadee.eu/Kaskadee/rpaextract/src/branch/master/sample ) directory.
It contains a single .png file to test the functionality of rpaextract.
## Dependencies
- [.NET Core 3.1](https://www.microsoft.com/net/download/core )
- [sharppickle](https://git.kaskadee.eu/Kaskadee/sharppickle ) (pickle deserialization)
- [SharpCompress](https://github.com/adamhathcock/sharpcompress ) (zlib decompression / provided by NuGet)
- [CommandLineParser](https://github.com/commandlineparser/commandline ) (command-line argument handling / provided by NuGet)

## License
rpaextract is licensed under the [European Union Public Licence v1.2](https://git.kaskadee.eu/Kaskadee/rpaextract/src/branch/master/LICENSE )