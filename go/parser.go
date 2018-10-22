package main

import (
	"bytes"
	"encoding/binary"
	"errors"
	"fmt"
	"github.com/golang-collections/collections/stack"
	"io"
	"os"
)

var unicodeString byte = 'X'
var shortBinaryString byte = 'U'
var binaryInteger byte = 'J'
var binaryLong byte = 0x8A
var binaryInput byte = 'q'
var longBinaryInput byte = 'r'
var endIndexPrefix byte = 0x87
var endIndex byte = 0x86

func Unpickle(data []byte) ([]ArchiveIndex, error) {
	// Prepare an empty slice of archive indices.
	var indices []ArchiveIndex

	// Validate pickle identifier and pickle version.
	reader := bytes.NewReader(data)
	protocolIdentifier, err := reader.ReadByte()
	if err != nil {
		return nil, err
	}
	protocolVersion, err := reader.ReadByte()
	if err != nil {
		return nil, err
	}
	if protocolIdentifier != 0x80 || protocolVersion != 2 {
		return nil, errors.New("specified pickle is invalid or not supported")
	}

	// Skip the next four bytes as their not relevant for parsing the pickle.
	reader.Seek(4, 1)

	// Prepare a new stack to store values.
	elementStack := stack.New()
	var skipElement bool
	for {
		// Read next marker byte and check for end of file.
		b, err := reader.ReadByte()
		if err == io.EOF {
			break
		} else if err != nil {
			return nil, err
		}

		if skipElement {
			if b != endIndexPrefix && b != endIndex && b != ']' {
				continue
			} else if b == ']' {
				skipElement = false
				reader.Seek(2, 1)
				continue
			}
		}

		skipElement = false
		switch b {
		case unicodeString:
			// Read length prefix to determine string length.
			length, err := readInteger(reader)
			if err != nil {
				return nil, err
			}

			// Create buffer to fit the string into.
			buffer := make([]byte, length)
			bytesRead, err := reader.Read(buffer)
			if err != nil {
				return nil, err
			} else if bytesRead != int(length) {
				return nil, errors.New("insufficient bytes left in stream")
			}
			// Push element to stack.
			elementStack.Push(buffer)
			skipElement = true
		case binaryLong:
			// Read length of integer.
			length, err := reader.ReadByte()
			if err != nil {
				return nil, err
			}

			// Create buffer to fit in integer.
			buffer := make([]byte, length)
			_, err = reader.Read(buffer)
			var number int
			switch length {
			case 4:
				number = int(binary.LittleEndian.Uint32(buffer))
			case 8:
				number = int(binary.LittleEndian.Uint64(buffer))
			default:
				return nil, errors.New(fmt.Sprintf("%d is not a valid binary input length", length))
			}
			// Push element to stack.
			elementStack.Push(number)
		case binaryInteger:
			number, err := readInteger(reader)
			if err != nil {
				return nil, err
			}
			// Push element to stack.
			elementStack.Push(number)
		case shortBinaryString:
			length, err := reader.ReadByte()
			if err != nil {
				return nil, err
			}

			buffer := make([]byte, length)
			readBytes, err := reader.Read(buffer)
			if err != nil {
				return nil, err
			} else if readBytes != int(length) {
				return nil, errors.New("insufficient bytes left in stream")
			}
			elementStack.Push(buffer)
		case longBinaryInput:
			reader.Seek(4, 1)
		case binaryInput:
			reader.Seek(1, 1)
		case endIndex, endIndexPrefix:
			var prefixObject interface{}
			if b == endIndexPrefix {
				prefixObject = elementStack.Pop()
			}
			lengthObject := elementStack.Pop()
			offsetObject := elementStack.Pop()
			pathObject := elementStack.Pop()

			if lengthObject == nil || offsetObject == nil || pathObject == nil || (b == endIndexPrefix && prefixObject == nil) {
				// Push valid popped values back to stack.
				if offsetObject != nil {
					elementStack.Push(offsetObject)
				}
				if lengthObject != nil {
					elementStack.Push(lengthObject)
				}
				if prefixObject != nil {
					elementStack.Push(prefixObject)
				}

				position, _ := reader.Seek(0, 1)
				fmt.Fprintf(os.Stdout, "(Warning) Failed to pop sufficient values from stack. (at mem-pos: %d)\n", position)
				continue
			}

			offset, err := castInteger(offsetObject)
			if err != nil {
				return nil, err
			}

			length, err := castInteger(lengthObject)
			if err != nil {
				return nil, err
			}

			indices = append(indices, ArchiveIndex{string(pathObject.([]byte)), int64(offset), int(length), prefixObject.([]byte)})
			reader.Seek(3, 1)
		}
	}

	return indices, nil
}