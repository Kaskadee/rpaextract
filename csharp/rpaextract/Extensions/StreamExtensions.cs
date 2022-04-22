using System.Buffers;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace rpaextract.Extensions {
    /// <summary>
    ///     Provides extension methods to simplify retrieving data from a <seealso cref="Stream"/>.
    /// </summary>
    public static class StreamExtensions {
        /// <summary>
        ///     Reads a string that is terminated by a new-line-character (\n) asynchronously.
        /// </summary>
        /// <param name="stream">The <see cref="Stream"/> to read the string from.</param>
        /// <param name="token">The <seealso cref="CancellationToken"/> to cancel the task.</param>
        /// <returns>The <see cref="string"/> that was read from the stream.</returns>
        /// <remarks>The position of the stream will be advanced to the point after the line-break character.</remarks>
        public static async Task<string> ReadLineAsync(this Stream stream, CancellationToken token = default) {
            token.ThrowIfCancellationRequested();
            var sb = new StringBuilder();
            // Rent memory from pool.
            using IMemoryOwner<byte> owner = MemoryPool<byte>.Shared.Rent(1);
            while (stream.Position != stream.Length) {
                await stream.ReadAsync(owner.Memory[..1], token);
                var c = (char)owner.Memory.Span[0];
                if (c == '\n')
                    return sb.ToString();
                sb.Append(c);
            }
            return sb.ToString();
        }

        /// <summary>
        ///     Reads all remaining bytes from the specified stream into a byte array.
        /// </summary>
        /// <param name="stream">The stream to read from.</param>
        /// <param name="token">The <seealso cref="CancellationToken"/> to cancel the task.</param>
        /// <returns>The read data from the specified stream.</returns>
        public static async Task<byte[]> ReadToEndAsync(this Stream stream, CancellationToken token = default) {
            await using var ms = new MemoryStream();
            // Copy contents of stream to memory.
            await stream.CopyToAsync(ms, token);
            return ms.ToArray();
        }
    }
}
