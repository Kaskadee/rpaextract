# rpaextract

An application for listing/extracting content from Ren'py archives. Written in C# 12 / .NET 8.0
Utilizes [sharppickle](https://github.com/Kaskadee/sharppickle ) to parse python's pickle format.

## Usage

```text
rpaextract 1.4.0
USAGE:
    rpaextract.exe [OPTIONS]

OPTIONS:
    -h, --help       Prints help information
    -v, --version    Prints version information
    -f, --archive    The path to the Ren'py (.rpa) archive
    -l, --list       Lists all files in the archive by printing the path and name to the standard output. Mutually exclusive with '-x'
    -x, --extract    Extracts all files from the archive to the disk. Mutually exclusive with '-l'
    -o, --output     The output directory to extract files to. Only works with '-x'
    -q, --quiet      Suppresses any output to the standard output. Mutually exclusive with '-v'
    -v, --verbose    Prints detailed information about the current operation of the program. Mutually exclusive with '-q'
```

## Sample archive

A sample RPA-3.0 ren'py archive can be found in the [sample](https://github.com/Kaskadee/rpaextract/blob/master/sample ) directory.
It contains a single .png file to test the functionality of rpaextract.

## Dependencies

- [.NET 8.0](https://dotnet.microsoft.com/en-us/download/dotnet/8.0/runtime )
- [sharppickle](https://github.com/Kaskadee/sharppickle ) (pickle deserialization)
- [Spectre.Console](https://spectreconsole.net/ ) (library to create console applications / provided by NuGet)

## License

rpaextract is licensed under the [European Union Public Licence v1.2](https://github.com/Kaskadee/rpaextract/blob/master/LICENSE )
