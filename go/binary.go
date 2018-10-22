package main

import (
	"bytes"
	"encoding/binary"
	"errors"
	"fmt"
)

// Reads the next integer from the byte reader and returns its value.
// The byte order of the integer is little endian.
func readInteger(reader *bytes.Reader) (int32, error) {
	// Create buffer large enough to fit in a whole 32-bit integer.
	buffer := make([]byte, 4)
	bytesRead, err := reader.Read(buffer)
	if err != nil {
		return 0, err
	} else if bytesRead != 4 {
		return 0, errors.New("binary: insufficient bytes left in stream")
	}

	// Convert byte array to integer.
	return int32(binary.LittleEndian.Uint32(buffer)), nil
}

// Selects the best matching integer type for the specified object and returns its value.
func castInteger(object interface{}) (int, error) {
	switch t := object.(type) {
	case int:
		return object.(int), nil
	case int32:
		return int(object.(int32)), nil
	case int64:
		return int(object.(int64)), nil
	default:
		return 0, fmt.Errorf("binary: invalid type for numeric value: %v", t)
	}
}