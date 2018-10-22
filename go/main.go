package main

import (
	"fmt"
	"os"
	path2 "path"
	"path/filepath"
	"strings"
)

func main() {
	// Get file name and command-line arguments.
	path := filepath.Base(os.Args[0])
	arguments := os.Args[1:]
	if len(arguments) == 0 {
		fmt.Println("(Info) Syntax:", path, "<archive>")
		os.Exit(1)
	}

	// Check if file exists and get file information.
	archivePath := arguments[len(arguments) - 1]
	archiveStat, err := os.Stat(archivePath)
	if os.IsNotExist(err) || archiveStat.IsDir() {
		fmt.Fprintf(os.Stderr,"(Error) Archive not found.\n")
		os.Exit(2)
	}

	// Wrap file information in archive structure.
	archive, err := NewArchive(archivePath)
	if err != nil {
		fmt.Fprintf(os.Stderr, "(Fatal) Failed to parse the specified RPA archive: %v\n", err)
		os.Exit(3)
	}
	defer archive.Close()

	// List file in archive.
	if containsArgument(arguments, "--list") || containsArgument(arguments, "-l") {
		list, err := archive.GetFiles()
		if err != nil {
			fmt.Fprintf(os.Stderr, "(Fatal) Failed to read file list from RPA archive: %v\n", err)
			os.Exit(4)
		}

		for i, v := range list {
			fmt.Printf("%v. %v\n", i + 1, v)
		}
		os.Exit(0)
		return
	}

	outputDirectory := fmt.Sprintf("rpaextract_%s", strings.TrimSuffix(archive.FileName, filepath.Ext(archive.FileName)))
	outputStat, err := os.Stat(outputDirectory)
	if err == nil && os.IsExist(err) && outputStat.IsDir() {
		fmt.Fprintf(os.Stderr, "(Error) Output directory already exists!\n")
		os.Exit(5)
	}

	// Create output directory.
	err = os.Mkdir(outputDirectory, os.ModePerm)
	if err != nil {
		fmt.Fprintf(os.Stderr, "(Error) Failed to create output directory: %v\n", err)
		os.Exit(6)
	}

	for _, v := range archive.Indices {
		data, err := archive.Read(&v)
		if err != nil {
			fmt.Fprintf(os.Stderr, "(Error) Failed to read file data for %s (%v)\n", v.FilePath, err)
			continue
		}

		f := path2.Join(outputDirectory, v.FilePath)
		err = os.MkdirAll(filepath.Dir(f), os.ModePerm)
		if err != nil {
			fmt.Fprintf(os.Stderr, "(Error) Failed to create sub-directory for %s\n", v.FilePath)
			break
		}

		file, err := os.Create(f)
		if err != nil {
			fmt.Fprintf(os.Stderr, "(Error) Failed to create file for %s\n", v.FilePath)
			break
		}

		_, err = file.Write(data)
		if err != nil {
			fmt.Fprintf(os.Stderr, "(Error) Failed to write contents for %s\n", v.FilePath)
			break
		}

		file.Close()
	}
	fmt.Println("Done.")
}

func containsArgument(arguments []string, arg string) bool {
	for _, v := range arguments {
		if strings.ToLower(v) == strings.ToLower(arg) {
			return true
		}
	}
	return false
}