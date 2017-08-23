/*
 * Simplified port of the original from Python - pickle.py (https://github.com/python/cpython/blob/master/Lib/pickle.py)
 * Tested with following RPA versions:
 * - RPA 3.0
 */

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace rpaextract.Pickle
{
    /// <summary>
    /// A simple pickle parser for deserializing RPA python pickles into C# objects.
    /// </summary>
    public sealed class PickleParser
    {
        /// <summary>
        /// The supported protocol version which can be unpickled by this parser.
        /// </summary>
        public const int SupportedPickleVersion = 2;
        /// <summary>
        /// The protocol identifier. The next byte indicates the pickle version. All pickles created with protocol 2.0 or greater have this at their beginning.
        /// </summary>
        private const char ProtocolIdentifier = '\x80';
        /// <summary>
        /// Indicates a following short string which is less than 256 bytes long. The first byte indicates the length of the string.
        /// </summary>
        private const char ShortBinaryString = 'U';
        /// <summary>
        /// Indicates a following UTF-8 string prefixed with four bytes indicating its length.
        /// </summary>
        private const char UnicodeString = 'X';
        /// <summary>
        /// Indicates a following four-byte signed integer. 
        /// </summary>
        private const char BinaryInteger = 'J';
        /// <summary>
        /// Indicates a following long value as a byte sequence.
        /// The python documentation of pickle.py is a little bit misleading. The actual value type is depending on the next byte indicating the length of the byte sequence (less than 256 bytes according to the documentation)
        /// In an RPA-Archive it seems like its always only four bytes long = integer value.
        /// </summary>
        private const char BinaryLong = '\x8A';
        /// <summary>
        /// Indicates that the stack top should be pushed to a memo.
        /// From Python documentation: "store stack top in memo; index is 1-byte arg"
        /// TODO Don't know what to do with this yet, so it will be skipped for the moment.
        /// </summary>
        private const char BinaryInput = 'q';
        /// <summary>
        /// Indicates that the stack top should be pushed to a memo.
        /// From Python documentation: "store stack top in memo; index is 4-byte arg"
        /// TODO Don't know what to do with this yet, so it will be skipped for the moment.
        /// </summary>
        private const char LongBinaryInput = 'r';
        /// <summary>
        /// In Python, when this marker has been reached, a 2-tuple will be created from the top-most items from the stack.
        /// In this case, the archive index will be created by popping the three top items from the stack. (Archive Index without Prefix)
        /// </summary>
        private const char EndIndex = '\x86';
        /// <summary>
        /// In Python, when this marker has been reached, a 3-tuple will be created from the top-most items from the stack.
        /// In this case, the archive index will be created by popping the four top items from the stack. (Archive Index with Prefix)
        /// </summary>
        private const char EndIndexPrefix = '\x87';
        /// <summary>
        /// Unpickles the specified RPA data.
        /// </summary>
        /// <param name="data">The data serialized as a python pickle retrieved from an RPA-Archive.</param>
        /// <returns>The list of extracted archive indices.</returns>
        /// <exception cref="NotSupportedException">The specified pickle is invalid or unsupported.</exception>
        public IEnumerable<ArchiveIndex> Unpickle(byte[] data)
        {
            var indices = new List<ArchiveIndex>();
            var stack = new Stack<object>();
            var skipFlag = false;
            // Create memory stream from byte array
            using (var ms = new MemoryStream(data))
            {
                // Read first byte (in pickle protocol 2.0 and above this indicates the pickle version)
                var proto = ms.ReadByte();
                // Check if pickle version is valid and supported
                if (proto != ProtocolIdentifier || ms.ReadByte() != SupportedPickleVersion)
                    throw new NotSupportedException("The specified pickle is invalid or unsupported.");
                // Skip 4 bytes (the data here isn't required for parsing the archive)
                // In Python the data is used to initialize some empty collections to store the read data.
                ms.Position += 4;
                while (ms.Position < ms.Length)
                {
                    // Read byte indicator
                    var b = ms.ReadByte();
                    // If skip flag is set to true, skip until an end marker has been reached or the "EMPTY_LIST" marker (])
                    // After each string the ']' marker can be found.
                    if (skipFlag)
                    {
                        if (b != EndIndexPrefix && b != EndIndex && b != ']')
                            continue;
                        if (b == ']')
                        {
                            // If the ']' marker was reached, skip the next two bytes.
                            skipFlag = false;
                            ms.Position += 2;
                            continue;
                        }
                    }
                    // Reset skip flag
                    skipFlag = false;
                    byte[] buffer;
                    switch (b)
                    {
                        case UnicodeString:
                            {
                                // Read length-prefix to determine string length
                                buffer = new byte[4];
                                ms.Read(buffer, 0, buffer.Length);
                                // Convert read bytes to an integer
                                var length = BitConverter.ToInt32(buffer, 0);
                                // Check for valid length here. If the parser fails this could return an invalid value which could lead to a memory leak.
                                if(length > sbyte.MaxValue)
                                // Read string bytes
                                buffer = new byte[length];
                                ms.Read(buffer, 0, buffer.Length);
                                // Push data to stack
                                stack.Push(buffer);
                                skipFlag = true;
                            }
                            break;
                        case BinaryLong:
                            {
                                // Read length-byte
                                var length = ms.ReadByte();
                                // Read byte-sequence
                                buffer = new byte[length];
                                ms.Read(buffer, 0, buffer.Length);
                                long number;
                                // Even if the read byte sequence is 4 bytes long and "always" an integer value make sure to accept long values
                                switch (length)
                                {
                                    case 4:
                                        number = BitConverter.ToInt32(buffer, 0);
                                        break;
                                    case 8:
                                        number = BitConverter.ToInt64(buffer, 0);
                                        break;
                                    default:
                                        throw new InvalidDataException($"{length} is not a valid binary input length.");
                                }
                                // Push read data to stack
                                stack.Push(number);
                            }
                            break;
                        case BinaryInteger:
                            // Read four bytes
                            buffer = new byte[4];
                            ms.Read(buffer, 0, buffer.Length);
                            // Push value to stack
                            var secondValue = BitConverter.ToInt32(buffer, 0);
                            stack.Push(secondValue);
                            break;
                        case ShortBinaryString:
                            {
                                // Read length-prefix as byte
                                var length = ms.ReadByte();
                                // Read string data
                                buffer = new byte[length];
                                ms.Read(buffer, 0, buffer.Length);
                                // Push data to stack
                                stack.Push(buffer);
                                skipFlag = true;
                            }
                            break;
                        case LongBinaryInput:
                            ms.Position += 4;
                            break;
                        case BinaryInput:
                            ms.Position += 1;
                            break;
                        case EndIndex:
                            {
                                // Create from index from top items without prefix
                                object offsetObj = null;
                                if (!stack.TryPop(out object lengthObj) || !stack.TryPop(out offsetObj) || !stack.TryPop(out object pathObj))
                                {
                                    // Push popped values back
                                    if (lengthObj != null)
                                        stack.Push(lengthObj);
                                    if (offsetObj != null)
                                        stack.Push(offsetObj);
                                    Console.WriteLine($"(Warning) Failed to pop sufficient values from stack. (at mem-pos: {ms.Position})");
                                    continue;
                                }
                                indices.Add(new ArchiveIndex((string)pathObj, Convert.ToInt64(offsetObj), Convert.ToInt32(lengthObj), null));
                                // Skip next three bytes
                                ms.Position += 3;
                            }
                            break;
                        case EndIndexPrefix:
                            {
                                // Create from index from top items with prefix
                                object offsetObj = null;
                                object lengthObj = null;
                                if (!stack.TryPop(out object prefixObj) || !stack.TryPop(out lengthObj) || !stack.TryPop(out offsetObj) || !stack.TryPop(out object pathObj))
                                {
                                    // Push popped values back
                                    if (prefixObj != null)
                                        stack.Push(prefixObj);
                                    if (lengthObj != null)
                                        stack.Push(lengthObj);
                                    if (offsetObj != null)
                                        stack.Push(offsetObj);
                                    Console.WriteLine($"(Warning) Failed to pop sufficient values from stack. (at mem-pos: {ms.Position})");
                                    continue;
                                }
                                indices.Add(new ArchiveIndex(Encoding.UTF8.GetString((byte[])pathObj), Convert.ToInt64(offsetObj), Convert.ToInt32(lengthObj), (byte[])prefixObj));
                                // Skip next three bytes
                                ms.Position += 3;
                            }
                            break;
                    }
                }
            }
            return indices;
        }
    }
}
