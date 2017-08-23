# rpaextract
---
An application for listing/extracting content from Ren'py archives. Written in C# 7.0 / .NET Core 2.0
## Usage
---
```
Syntax: dotnet rpaextract.dll [--list] <archive>
--list or -l: List all files in the archive; if excluded the archive will be extracted
<archive>: Path to the archive
```
## Dependencies
---
- [.NET Core 2.0](https://www.microsoft.com/net/download/core)
- [SharpCompress](https://github.com/adamhathcock/sharpcompress) (zlib decompression / provided by NuGet)