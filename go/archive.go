package main

import (
	"bufio"
	"compress/zlib"
	"errors"
	"io/ioutil"
	"os"
	"path/filepath"
	"sort"
	"strconv"
	"strings"
)

// Represents an file index in an specific RPA archive.
// It contains meta information about the file e.g. the relative file name, the offset in the archive and the length in bytes.
type ArchiveIndex struct {
	FilePath string
	Offset int64
	Length int
	Prefix []byte
}

// Represents a whole RPA archive including all its indices.
type Archive struct {
	FileName string
	Version int
	Indices []ArchiveIndex
	handle *os.File
}

// Returns a value indicating whether the archive is supported and valid.
// The function performs a simple version check for an RPA-2.0 and an RPA-3.0 archive.
func (archive Archive) IsValid() bool {
	return archive.Version == 2 || archive.Version == 3
}

// Checks whether the specified archive index is located within the archive.
func (archive Archive) ContainsIndex(index *ArchiveIndex) bool {
	// Check if index pointer is valid.
	if index == nil {
		return false
	}

	// Compare element references with specified reference.
	for _, a := range archive.Indices {
		if a.FilePath == index.FilePath && a.Offset == index.Offset && a.Length == index.Length {
			return true
		}
	}
	return false
}

// Returns an array of the relative paths of all files located within the archive.
// The list is sorted alphabetically by the function.
func (archive *Archive) GetFiles() ([]string, error) {
	if !archive.IsValid() {
		return nil, errors.New("invalid archive version")
	}

	// Create slice of file paths from archive indices.
	list := make([]string, len(archive.Indices))
	for i := range list {
		list[i] = archive.Indices[i].FilePath
	}

	// Sort slice alphabetically.
	sort.Strings(list)
	return list, nil
}

// Reads the specified file from the archive.
// If the file handle of the archive was not opened at the time of the call, the file will be opened in read-only mode.
// If successful the function will return the file contents of the specified file.
func (archive *Archive) Read(index *ArchiveIndex) ([]byte, error) {
	// Check if file exists and is loaded.
	if index == nil || !archive.ContainsIndex(index) {
		return nil, errors.New("index cannot be nil and must be valid")
	}

	// Open archive file in read-only mode.
	if archive.handle == nil {
		handle, err := os.Open(archive.FileName)
		if err != nil {
			return nil, err
		}
		archive.handle = handle
	}

	// Read amount of bytes from the file offset.
	data := make([]byte, index.Length - len(index.Prefix))
	bytesRead, err := archive.handle.ReadAt(data, index.Offset)
	if err != nil {
		return nil, err
	}

	// Return complete data.
	return append(index.Prefix, data[:bytesRead]...), nil
}

// Closes the open file handle of the archive.
// If the file handle was closed at the time of the call, nil will be returned.
func (archive *Archive) Close() error {
	if archive.handle != nil {
		return archive.handle.Close()
	}
	return nil
}

// Creates a new representation of an RPA archive from the specified file.
// Returns the pointer to the newly allocated instance.
func NewArchive(path string) (*Archive, error) {
	// Check if file exists and get file information.
	stat, err := os.Stat(path)
	if os.IsNotExist(err) {
		return nil, err
	}
	if stat.IsDir() {
		return nil, errors.New("archive is not a file")
	}

	// Check if file is long enough.
	if stat.Size() < 51 {
		return nil, errors.New("file size is invalid")
	}

	// Try to open archive in read-only mode.
	file, err := os.Open(path)
	if err != nil {
		return nil, err
	}

	// Determine archive version.
	reader := bufio.NewReader(file)
	header, err := reader.ReadString('\n')
	if err != nil {
		return nil, err
	}

	var version int
	if strings.HasPrefix(header, "RPA-2.0") {
		version = 2
	} else if strings.HasPrefix(header, "RPA-3.0") {
		version = 3
	} else {
		return nil, errors.New("invalid archive version")
	}

	// Parse offset of file tree.
	splitted := strings.Split(header, "\x20")
	if len(splitted) < 2 {
		return nil, errors.New("invalid header")
	}
	tmp := splitted[1][:len(splitted[1])]
	offset, err := strconv.ParseInt(tmp, 16, 64)
	if err != nil {
		return nil, err
	}

	// Seek to file tree of archive.
	file.Seek(offset, 0)
	stream, err := zlib.NewReader(file)
	if err != nil {
		return nil, err
	}

	// Decompress file tree using zlib.
	uncompressed, err := ioutil.ReadAll(stream)
	if err != nil {
		return nil, err
	}

	// Unpickle the file tree and parse file indices.
	indices, err := Unpickle(uncompressed)
	if err != nil {
		return nil, err
	}

	// Apply deobfuscation of offset and length if necessary.
	if version == 3 {
		// Calculate deobfuscation key.
		key := 0
		for _, v := range splitted[2:] {
			parsed, err := strconv.ParseInt(v[:len(v) - 1], 16, 32)
			if err != nil {
				return nil, err
			}
			key ^= int(parsed)
		}

		// Apply deobfuscation.
		for i, v := range indices {
			v.Offset = v.Offset ^ int64(key)
			v.Length = v.Length ^ key
			indices[i] = v
		}
	}

	// Create instance of archive structure.
	return &Archive{filepath.Base(path),version, indices, file}, nil
}