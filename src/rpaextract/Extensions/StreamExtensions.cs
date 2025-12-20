// rpaextract - StreamExtensions.cs
// Copyright (C) 2017-2026 Fabian Creutz.
// 
// Licensed under the EUPL, Version 1.2 or – as soon they will be approved by the
// European Commission - subsequent versions of the EUPL (the "Licence");
// 
// You may not use this work except in compliance with the Licence.
// You may obtain a copy of the Licence at:
// 
// https://joinup.ec.europa.eu/software/page/eupl
// 
// Unless required by applicable law or agreed to in writing, software distributed under the Licence is distributed on an "AS IS" basis,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the Licence for the specific language governing permissions and limitations under the Licence.

using System.Text;
using CommunityToolkit.HighPerformance.Buffers;

namespace rpaextract.Extensions;

/// <summary>
///     Provides extension methods to simplify retrieving data from a <see cref="Stream" />.
/// </summary>
public static class StreamExtensions {
    extension(Stream stream) {
        /// <summary>
        ///     Reads a string terminated by a new-line-character (\n) asynchronously.
        /// </summary>
        /// <param name="token">The <seealso cref="CancellationToken" /> to cancel the task.</param>
        /// <returns>The <see cref="string" /> that was read from the stream.</returns>
        /// <remarks>The position of the stream will be advanced to the point after the line-break character.</remarks>
        public async Task<string> ReadLineAsync(CancellationToken token = default) {
            token.ThrowIfCancellationRequested();
            StringBuilder sb = new();
            using MemoryOwner<byte> owner = MemoryOwner<byte>.Allocate(1);
            while (stream.Position != stream.Length) {
                int n = await stream.ReadAsync(owner.Memory, token);
                if (n != 1)
                    break;
                var c = (char)owner.Span[0];
                if (c == '\n')
                    return sb.ToString();
                sb.Append(c);
            }

            return sb.ToString();
        }

        /// <summary>
        ///     Reads all remaining bytes from the specified stream into a byte array.
        /// </summary>
        /// <param name="token">The <seealso cref="CancellationToken" /> to cancel the task.</param>
        /// <returns>The read data from the specified stream.</returns>
        public async Task<byte[]> ReadToEndAsync(CancellationToken token = default) {
            await using var ms = new MemoryStream(stream.CanSeek ? int.CreateChecked(stream.Length) : 0);
            await stream.CopyToAsync(ms, token);
            return ms.ToArray();
        }
    }
}
