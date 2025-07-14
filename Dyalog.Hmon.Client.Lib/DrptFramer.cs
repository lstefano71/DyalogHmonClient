using System.Net.Sockets;
using System.Text;
using System;
using System.Threading.Tasks;

namespace Dyalog.Hmon.Client.Lib
{
    /// <summary>
    /// Handles DRP-T framing for protocol messages (RFC 0001).
    /// </summary>
    internal class DrptFramer
    {
        private readonly byte[] _magic;
        private const int HeaderSize = 8; // 4 bytes length + 4 bytes magic

        public DrptFramer(string magic)
        {
            if (magic == null || magic.Length != 4)
                throw new ArgumentException("Magic number must be 4 ASCII characters", nameof(magic));
            _magic = Encoding.ASCII.GetBytes(magic);
        }

        public async Task WriteFrameAsync(NetworkStream stream, byte[] payload, CancellationToken ct)
        {
            var totalLength = HeaderSize + payload.Length;
            var lengthBytes = BitConverter.GetBytes(totalLength);
            if (BitConverter.IsLittleEndian) Array.Reverse(lengthBytes);
            await stream.WriteAsync(lengthBytes, ct);
            await stream.WriteAsync(_magic, ct);
            await stream.WriteAsync(payload, ct);
        }

        public async Task<byte[]> ReadFrameAsync(NetworkStream stream, CancellationToken ct)
        {
            // Read total length (4 bytes)
            var lengthBytes = new byte[4];
            await ReadExactAsync(stream, lengthBytes, ct);
            if (BitConverter.IsLittleEndian) Array.Reverse(lengthBytes);
            int totalLength = BitConverter.ToInt32(lengthBytes, 0);

            // Read magic (4 bytes)
            var magic = new byte[4];
            await ReadExactAsync(stream, magic, ct);
            if (!magic.AsSpan().SequenceEqual(_magic))
                throw new InvalidOperationException($"Invalid magic number. Expected '{Encoding.ASCII.GetString(_magic)}', got '{Encoding.ASCII.GetString(magic)}'");

            // Read payload
            int payloadLen = totalLength - HeaderSize;
            var payload = new byte[payloadLen];
            await ReadExactAsync(stream, payload, ct);
            return payload;
        }

        public static async Task ReadExactAsync(NetworkStream stream, byte[] buffer, CancellationToken ct)
        {
            int offset = 0;
            while (offset < buffer.Length)
            {
                int read = await stream.ReadAsync(buffer.AsMemory(offset, buffer.Length - offset), ct);
                if (read == 0) throw new IOException("Unexpected end of stream during frame read");
                offset += read;
            }
        }

        public static int GetHeaderSize() => HeaderSize;
        public byte[] Magic => _magic;
    }
}
