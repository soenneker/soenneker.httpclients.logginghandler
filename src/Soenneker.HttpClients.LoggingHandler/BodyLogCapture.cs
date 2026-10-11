using Microsoft.Extensions.Logging;
using System;
using System.IO;
using System.Text;

namespace Soenneker.HttpClients.LoggingHandler;

internal sealed class BodyLogCapture(ILogger logger, LogLevel level, string arrow, int characterLimit)
{
    private readonly MemoryStream _prefix = new();
    private readonly long _byteLimit = (long) characterLimit * 4 + 8;
    private bool _completed;

    public void Append(ReadOnlySpan<byte> bytes)
    {
        if (_completed)
            return;

        int count = (int) Math.Min(bytes.Length, _byteLimit - _prefix.Length);
        _prefix.Write(bytes[..count]);
        if (_prefix.Length == _byteLimit)
            Complete();
    }

    public void Complete()
    {
        if (_completed)
            return;

        _completed = true;
        try
        {
            _prefix.Position = 0;
            using var reader = new StreamReader(_prefix, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
            string text = reader.ReadToEnd();
            string body = text.Length > characterLimit
                ? string.Concat(text.AsSpan(0, characterLimit), "...(truncated)")
                : text;
            logger.Log(level, "{Arrow} Body: {Body}", arrow, body);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "{Arrow} Failed to read body", arrow);
        }
        finally
        {
            _prefix.Dispose();
        }
    }
}
