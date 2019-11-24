using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace rpaextract.Extensions {
    /// <summary>
    ///     Provides methods for extending the <see cref="Stream" /> class and its derivatives.
    /// </summary>
    public static class StreamExtensions {
        /// <summary>
        ///     Reads a string, from the specified stream, that is terminated by a line-break.
        /// </summary>
        /// <param name="stream">The stream to read the string from.</param>
        /// <returns>The read string without the line-break at the end of the string.</returns>
        /// <remarks>The position of the stream will be advanced to the point after the line-break character.</remarks>
        public static string ReadLine(this Stream stream) {
            var sb = new StringBuilder();
            // Search until line-break is found or end of stream has been reached
            while (stream.Position < stream.Length) {
                var newChar = (char) stream.ReadByte();
                if (newChar == '\n')
                    return sb.ToString();
                sb.Append(newChar);
            }

            // Reached end of stream and no line-break has been found.
            return sb.ToString();
        }

        /// <summary>
        ///     Reads all bytes until the end of stream, from the current position of the stream, has been reached.
        /// </summary>
        /// <param name="stream">The stream to read from.</param>
        public static byte[] ReadToEnd(this Stream stream) {
            using var ms = new MemoryStream();
            // Copy contents of stream to memory.
            stream.CopyTo(ms);
            return ms.ToArray();
        }

        /// <summary>
        ///     Reads all remaining bytes from the specified stream into a byte array.
        /// </summary>
        /// <param name="stream">The stream to read from.</param>
        /// <returns>The read data from the specified stream.</returns>
        public static async Task<byte[]> ReadToEndAsync(this Stream stream) {
            using var ms = new MemoryStream();
            // Copy contents of stream to memory.
            await stream.CopyToAsync(ms);
            return ms.ToArray();
        }
    }
}
