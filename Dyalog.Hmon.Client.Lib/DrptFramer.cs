using Serilog;

using System.Text;

namespace Dyalog.Hmon.Client.Lib
{
  /// <summary>
  /// Handles DRP-T framing for protocol messages (RFC 0001).
  /// </summary>
  internal class DrptFramer
  {
    private readonly byte[] _magic;
    private readonly ILogger _logger;
    private const int HeaderSize = 8; // 4 bytes length + 4 bytes magic

    public DrptFramer(string magic)
    {
      if (magic == null || magic.Length != 4)
        throw new ArgumentException("Magic number must be 4 ASCII characters", nameof(magic));
      _magic = Encoding.ASCII.GetBytes(magic);
      _logger = Serilog.Log.ForContext<DrptFramer>();
    }

    public async Task WriteFrameAsync(Stream stream, string text, CancellationToken ct)
    {
      var payload = Encoding.UTF8.GetBytes(text);
      await WriteFrameAsync(stream, payload, ct);
      _logger.Debug("{direction} Message: {Raw}", "SENT", text);
    }

    public async Task WriteFrameAsync(Stream stream, byte[] payload, CancellationToken ct)
    {
      var totalLength = HeaderSize + payload.Length;
      var lengthBytes = BitConverter.GetBytes(totalLength);
      if (BitConverter.IsLittleEndian) Array.Reverse(lengthBytes);
      await stream.WriteAsync(lengthBytes, ct);
      await stream.WriteAsync(_magic, ct);
      await stream.WriteAsync(payload, ct);
    }

    public static int GetHeaderSize() => HeaderSize;
    public byte[] Magic => _magic;
  }
}
