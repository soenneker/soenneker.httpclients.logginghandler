using Microsoft.Extensions.Logging;
using System;
using System.Buffers;
using System.Text;

namespace Soenneker.HttpClients.LoggingHandler;

internal sealed class BodyLogCapture(ILogger logger, LogLevel level, string arrow, int characterLimit)
{
    private byte[]? _prefix;
    private int _length;
    private readonly long _byteLimit = (long) characterLimit * 4 + 8;
    public bool IsCompleted { get; private set; }

    public void Append(ReadOnlySpan<byte> bytes)
    {
        if (IsCompleted || bytes.IsEmpty)
            return;

        int count = (int) Math.Min(bytes.Length, _byteLimit - _length);
        int required = checked(_length + count);
        if (_prefix is null || required > _prefix.Length)
        {
            byte[] buffer = ArrayPool<byte>.Shared.Rent(required);
            if (_prefix is not null)
            {
                _prefix.AsSpan(0, _length).CopyTo(buffer);
                ArrayPool<byte>.Shared.Return(_prefix, clearArray: true);
            }
            _prefix = buffer;
        }

        bytes[..count].CopyTo(_prefix.AsSpan(_length));
        _length = required;
        if (_length == _byteLimit)
            Complete();
    }

    public void Complete()
    {
        if (IsCompleted)
            return;

        IsCompleted = true;
        char[]? chars = null;
        try
        {
            if (!logger.IsEnabled(level))
                return;

            ReadOnlySpan<byte> bytes = _prefix.AsSpan(0, _length);
            Encoding encoding = GetEncoding(ref bytes);
            // Decode only enough characters to distinguish a complete preview from a truncated one.
            int capacity = (int) Math.Min((long) characterLimit + 2, encoding.GetMaxCharCount(bytes.Length));
            chars = ArrayPool<char>.Shared.Rent(capacity);
            encoding.GetDecoder().Convert(bytes, chars.AsSpan(0, capacity), flush: true,
                out _, out int written, out _);
            string body = written > characterLimit
                ? string.Concat(chars.AsSpan(0, characterLimit), "...(truncated)")
                : new string(chars, 0, written);
            HttpLog.Body(logger, level, arrow, BodyLogText.EscapeControls(body));
        }
        catch (Exception ex)
        {
            HttpLog.BodyFailure(logger, ex, arrow);
        }
        finally
        {
            if (chars is not null)
                ArrayPool<char>.Shared.Return(chars, clearArray: true);
            if (_prefix is not null)
            {
                ArrayPool<byte>.Shared.Return(_prefix, clearArray: true);
                _prefix = null;
            }
        }
    }

    private static Encoding GetEncoding(ref ReadOnlySpan<byte> bytes)
    {
        // Match the BOM detection previously supplied by StreamReader.
        Encoding encoding = Encoding.UTF8;
        int skip = 0;
        if (bytes.StartsWith((ReadOnlySpan<byte>) [0xEF, 0xBB, 0xBF]))
            skip = 3;
        else if (bytes.StartsWith((ReadOnlySpan<byte>) [0xFF, 0xFE, 0x00, 0x00]))
        {
            encoding = Encoding.UTF32;
            skip = 4;
        }
        else if (bytes.StartsWith((ReadOnlySpan<byte>) [0x00, 0x00, 0xFE, 0xFF]))
        {
            encoding = BigEndianUtf32;
            skip = 4;
        }
        else if (bytes.StartsWith((ReadOnlySpan<byte>) [0xFF, 0xFE]))
        {
            encoding = Encoding.Unicode;
            skip = 2;
        }
        else if (bytes.StartsWith((ReadOnlySpan<byte>) [0xFE, 0xFF]))
        {
            encoding = Encoding.BigEndianUnicode;
            skip = 2;
        }
        bytes = bytes[skip..];
        return encoding;
    }

    private static readonly Encoding BigEndianUtf32 = new UTF32Encoding(bigEndian: true, byteOrderMark: true);
}
