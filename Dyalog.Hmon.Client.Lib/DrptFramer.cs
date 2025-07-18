using Serilog;

using System.Buffers;
using System.IO.Pipelines;
using System.Text;

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

    /// <summary>
    /// Initializes a new DRPT framer for protocol message handling.
    /// </summary>
    /// <param name="magic">4-character magic string for protocol framing.</param>
    /// <param name="stream">Underlying stream for reading/writing.</param>
    public DrptFramer(string magic, Stream stream)
    {
      _stream = stream;
      _reader = PipeReader.Create(stream);

      if (magic == null || magic.Length != 4)
        throw new ArgumentException("Magic number must be 4 ASCII characters", nameof(magic));
      _magic = Encoding.ASCII.GetBytes(magic);
      _logger = Log.ForContext<DrptFramer>();
    }

    /// <summary>
    /// Writes a text message as a DRPT-framed protocol message.
    /// </summary>
    /// <param name="text">Message text.</param>
    /// <param name="ct">Cancellation token.</param>
    public async Task WriteFrameAsync(string text, CancellationToken ct)
    {
      var payload = Encoding.UTF8.GetBytes(text);
      await WriteFrameAsync(payload, ct);
      _logger.Debug("{direction} Message: {Raw}", "SENT", text);
    }

    /// <summary>
    /// Writes a byte payload as a DRPT-framed protocol message.
    /// </summary>
    /// <param name="payload">Message payload.</param>
    /// <param name="ct">Cancellation token.</param>
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

    /// <summary>
    /// Performs the DRPT handshake sequence with the remote endpoint.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>True if handshake succeeds; otherwise false.</returns>
    public async Task<bool> PerformHandshakeAsync(CancellationToken ct)
    {
      await WriteFrameAsync(_supportedProtocol, ct);

      if (!await ExpectStringAsync(_supportedProtocol, ct)) {
        _logger.Error("Handshake failed: expected '{supportedprotocol}'", _supportedProtocol);
        return false;
      }
      await WriteFrameAsync(_usingProtocol, ct);

      if (!await ExpectStringAsync(_usingProtocol, ct)) {
        _logger.Error("Handshake failed: expected '{usingprotocol}'", _usingProtocol);
        return false;
      }
      return true;
    }

    /// <summary>
    /// Awaits a protocol message and checks if it matches the expected string.
    /// </summary>
    /// <param name="expected">Expected message string.</param>
    /// <param name="ct">Cancellation token.</param>
    public Task<bool> ExpectStringAsync(string expected, CancellationToken ct = default) =>
      ReadMessageAsStringAsync(msg => SequenceEqualsString(msg, expected), ct);

    /// <summary>
    /// Reads a protocol message and processes it using the provided delegate.
    /// </summary>
    /// <typeparam name="T">Return type of the process delegate.</typeparam>
    /// <param name="process">Delegate to process the message.</param>
    /// <param name="ct">Cancellation token.</param>
    public async Task<T> ReadMessageAsStringAsync<T>(Func<ReadOnlySequence<byte>, T> process, CancellationToken ct = default)
    {
      while (true) {
        ReadResult result = await _reader.ReadAsync(ct);
        ReadOnlySequence<byte> buffer = result.Buffer;
        if (TryReadMessage(ref buffer, out var message)) {
          var rvalue = process(message);
          _reader.AdvanceTo(buffer.Start);
          return rvalue;
        }
        if (result.IsCompleted && buffer.IsEmpty) {
          throw new EndOfStreamException("No more data available in the stream.");
        }
      }
    }

    /// <summary>
    /// Attempts to read a complete DRPT protocol message from the buffer.
    /// </summary>
    /// <param name="buffer">Input buffer.</param>
    /// <param name="message">Output message sequence.</param>
    /// <returns>True if a complete message was read; otherwise false.</returns>
    public bool TryReadMessage(ref ReadOnlySequence<byte> buffer, out ReadOnlySequence<byte> message)
    {
      message = default;
      if (buffer.Length < 4) return false; // Not enough for length prefix

      var lengthBytes = buffer.Slice(0, 4).ToArray();
      if (BitConverter.IsLittleEndian) Array.Reverse(lengthBytes);
      var messageLength = BitConverter.ToInt32(lengthBytes, 0);

      int headerOffset = 4; // For length prefix

      if (buffer.Length < 8) return false; // Not enough for length + magic
      var magicBytes = buffer.Slice(headerOffset, headerOffset).ToArray();
      if (!magicBytes.AsSpan().SequenceEqual(_magic)) {
        throw new InvalidOperationException($"Invalid handshake magic number encountered. Expected '{Encoding.ASCII.GetString(_magic)}', got '{Encoding.ASCII.GetString(magicBytes)}'");
      }
      headerOffset = 8;

      if (buffer.Length < messageLength) return false; // Not enough for full message

      message = buffer.Slice(headerOffset, messageLength - headerOffset);
      buffer = buffer.Slice(message.End);

      _logger.Debug("{direction} Message: {Raw}", "RECV", Encoding.UTF8.GetString(message.ToArray()));
      return true;
    }

    /// <summary>
    /// Reads the next DRPT protocol message as a byte array.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    public async Task<byte[]> ReadNextMessageAsync(CancellationToken ct)
    {
      while (true) {
        ReadResult result = await _reader.ReadAsync(ct);
        ReadOnlySequence<byte> buffer = result.Buffer;
        if (TryReadMessage(ref buffer, out var message)) {
          var decoded = message.ToArray();
          _reader.AdvanceTo(buffer.Start, buffer.End);
          return decoded;
        }
        if (result.IsCompleted && buffer.IsEmpty) {
          throw new EndOfStreamException("No more data available in the stream.");
        }
      }
    }

    /// <summary>
    /// Reads the next DRPT protocol message and processes it using the provided delegate.
    /// </summary>
    /// <typeparam name="TResult">Return type of the process delegate.</typeparam>
    /// <param name="process">Delegate to process the message.</param>
    /// <param name="ct">Cancellation token.</param>
    public async Task<TResult> ReadNextMessageAsync<TResult>(Func<ReadOnlySequence<byte>, TResult> process, CancellationToken ct)
    {
      while (true) {
        ReadResult result = await _reader.ReadAsync(ct);
        ReadOnlySequence<byte> buffer = result.Buffer;
        if (TryReadMessage(ref buffer, out var message)) {
          var resultValue = process(message);
          _reader.AdvanceTo(buffer.Start, buffer.End);
          return resultValue;
        }
        if (result.IsCompleted && buffer.IsEmpty) {
          throw new EndOfStreamException("No more data available in the stream.");
        }
      }
    }

    /// <summary>
    /// Compares a byte sequence to a string for protocol equality.
    /// </summary>
    /// <param name="sequence">Byte sequence.</param>
    /// <param name="value">String value.</param>
    /// <returns>True if equal; otherwise false.</returns>
    public static bool SequenceEqualsString(ReadOnlySequence<byte> sequence, string value)
    {
      var utf8Bytes = Encoding.UTF8.GetBytes(value);
      if (sequence.Length != utf8Bytes.Length) return false;

      if (sequence.IsSingleSegment) {
        return sequence.FirstSpan.SequenceEqual(utf8Bytes);
      } else {
        var reader = new SequenceReader<byte>(sequence);
        foreach (var b in utf8Bytes) {
          if (!reader.TryRead(out var seqByte) || seqByte != b)
            return false;
        }
        return !reader.TryRead(out _); // Ensure no extra bytes
      }
    }
  }
}
