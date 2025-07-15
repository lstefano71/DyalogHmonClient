using Serilog;
using System.Buffers;
using System.IO.Pipelines;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Dyalog.Hmon.Client.Lib
{
  /// <summary>
  /// Handles DRP-T framing for protocol messages (RFC 0001) and generic handshake/message reading.
  /// </summary>
  internal class DrptFramer
  {
    private const string _supportedProtocol = "SupportedProtocols=2";
    private const string _usingProtocol = "UsingProtocol=2";

    private readonly byte[] _magic;
    private readonly ILogger _logger;
    private readonly PipeReader _reader;
    private readonly Stream _stream;
    private const int HeaderSize = 8; // 4 bytes length + 4 bytes magic

    public DrptFramer(string magic, Stream stream)
    {
      _stream = stream;
      _reader = PipeReader.Create(stream);
      
      if (magic == null || magic.Length != 4)
        throw new ArgumentException("Magic number must be 4 ASCII characters", nameof(magic));
      _magic = Encoding.ASCII.GetBytes(magic);
      _logger = Log.ForContext<DrptFramer>();
    }

    public async Task WriteFrameAsync(string text, CancellationToken ct)
    {
      var payload = Encoding.UTF8.GetBytes(text);
      await WriteFrameAsync(payload, ct);
      _logger.Debug("{direction} Message: {Raw}", "SENT", text);
    }

    public async Task WriteFrameAsync(byte[] payload, CancellationToken ct)
    {
      var totalLength = HeaderSize + payload.Length;
      var lengthBytes = BitConverter.GetBytes(totalLength);
      if (BitConverter.IsLittleEndian) Array.Reverse(lengthBytes);
      await _stream.WriteAsync(lengthBytes, ct);
      await _stream.WriteAsync(_magic, ct);
      await _stream.WriteAsync(payload, ct);
    }

    public static int GetHeaderSize() => HeaderSize;
    public byte[] Magic => _magic;

    // --- Generic DRPT message reading and handshake logic ---

    public async Task<bool> PerformHandshakeAsync(CancellationToken ct)
    {
      await WriteFrameAsync(_supportedProtocol, ct);
      var msg = await ReadMessageAsStringAsync(ct);
      if (msg != _supportedProtocol) {
        _logger.Error("Handshake failed: expected '{supportedprotocol}', got '{Message}'", _supportedProtocol, msg);
        return false;
      }
      await WriteFrameAsync(_usingProtocol, ct);
      msg = await ReadMessageAsStringAsync(ct);
      if (msg != _usingProtocol) {
        _logger.Error("Handshake failed: expected '{usingprotocol}', got '{Message}'", _usingProtocol, msg);
        return false;
      }
      return true;
    }

    public async Task<string> ReadMessageAsStringAsync(CancellationToken ct)
    {
      while (true)
      {
        ReadResult result = await _reader.ReadAsync(ct);
        ReadOnlySequence<byte> buffer = result.Buffer;
        if (TryReadMessage(ref buffer, out var message))
        {
          var messageBytes = Encoding.UTF8.GetString(message.ToArray());
          _reader.AdvanceTo(buffer.Start);
          return messageBytes;
        }
        if (result.IsCompleted && buffer.IsEmpty)
        {
          throw new EndOfStreamException("No more data available in the stream.");
        }
      }
    }

    public bool TryReadMessage(ref ReadOnlySequence<byte> buffer, out ReadOnlySequence<byte> message)
    {
      message = default;
      if (buffer.Length < 4) return false; // Not enough for length prefix

      var lengthBytes = buffer.Slice(0, 4).ToArray();
      if (BitConverter.IsLittleEndian) Array.Reverse(lengthBytes);
      var messageLength = BitConverter.ToInt32(lengthBytes, 0);

      int headerOffset = 4; // For length prefix

      if (buffer.Length < 8) return false; // Not enough for length + magic
      var magicBytes = buffer.Slice(4, 4).ToArray();
      if (!magicBytes.AsSpan().SequenceEqual(_magic))
      {
        throw new InvalidOperationException($"Invalid handshake magic number encountered. Expected '{Encoding.ASCII.GetString(_magic)}', got '{Encoding.ASCII.GetString(magicBytes)}'");
      }
      headerOffset = 8;

      if (buffer.Length < messageLength) return false; // Not enough for full message
      
      message = buffer.Slice(headerOffset, messageLength - headerOffset);
      buffer = buffer.Slice(message.End);

      _logger.Debug("{direction} Message: {Raw}", "RECV", Encoding.UTF8.GetString(message.ToArray()));
      return true;
    }

    public async Task<byte[]> ReadNextMessageAsync(CancellationToken ct)
    {
      while (true)
      {
        ReadResult result = await _reader.ReadAsync(ct);
        ReadOnlySequence<byte> buffer = result.Buffer;
        if (TryReadMessage(ref buffer, out var message))
        {
          var decoded = message.ToArray();
          _reader.AdvanceTo(buffer.Start, buffer.End);
          return decoded;
        }
        if (result.IsCompleted && buffer.IsEmpty)
        {
          throw new EndOfStreamException("No more data available in the stream.");
        }
      }
    }
  }
}
