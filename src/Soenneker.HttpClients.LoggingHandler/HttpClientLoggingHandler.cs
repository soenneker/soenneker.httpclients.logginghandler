using Microsoft.Extensions.Logging;
using Soenneker.Extensions.Enumerable.String;
using Soenneker.Extensions.Stream;
using Soenneker.Extensions.Task;
using Soenneker.Extensions.ValueTask;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Soenneker.HttpClients.LoggingHandler;

/// <summary>
/// A delegating handler that logs HTTP request and response details, including headers and optionally bodies,  
/// for diagnostic and debugging purposes.
/// </summary>
public sealed class HttpClientLoggingHandler : DelegatingHandler
{
    private readonly ILogger _logger;
    private readonly HttpClientLoggingOptions _opts;
    private readonly HashSet<string> _redactions;

    public HttpClientLoggingHandler(ILogger logger, HttpClientLoggingOptions? options)
    {
        _logger = logger;
        _opts = options ?? new HttpClientLoggingOptions();
        _redactions = new HashSet<string>(_opts.RedactedHeaders ?? [], StringComparer.OrdinalIgnoreCase);
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        if (!_logger.IsEnabled(_opts.LogLevel))
            return base.SendAsync(request, ct);

        return SendAndLog(request, ct);
    }

    private async Task<HttpResponseMessage> SendAndLog(HttpRequestMessage request, CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();
        _logger.Log(_opts.LogLevel, "→ {Method} {Uri}", request.Method, request.RequestUri);

        if (_opts.LogRequestHeaders)
        {
            LogHeaders("→", request.Headers);
            if (request.Content?.Headers != null)
                LogHeaders("→", request.Content.Headers);
        }

        if (_opts.LogRequestBody && request.Content != null)
            await LogBody("→", request.Content, ct).NoSync();

        HttpResponseMessage response;

        try
        {
            response = await base.SendAsync(request, ct).NoSync();
        }
        catch (Exception ex)
        {
            sw.Stop();
            _logger.LogError(ex, "✗ {Method} {Uri} failed after {Elapsed}ms", request.Method, request.RequestUri, sw.ElapsedMilliseconds);
            throw;
        }

        sw.Stop();
        _logger.Log(_opts.LogLevel, "← {StatusCode} in {Elapsed}ms for {Method} {Uri}",
            response.StatusCode, sw.ElapsedMilliseconds, request.Method, request.RequestUri);

        if (_opts.LogResponseHeaders)
        {
            LogHeaders("←", response.Headers);

            if (response.Content?.Headers != null)
                LogHeaders("←", response.Content.Headers);
        }

        if (_opts.LogResponseBody && response.Content != null)
            await LogBody("←", response.Content, ct).NoSync();

        return response;
    }

    private void LogHeaders(string arrow, HttpHeaders headers)
    {
        foreach (var header in headers)
        {
            string value = _redactions.Contains(header.Key)
                ? "***"
                : header.Value.ToCommaSeparatedString(true);

            _logger.Log(_opts.LogLevel, "{Arrow} Header {Key}: {Value}", arrow, header.Key, value);
        }
    }

    private async ValueTask LogBody(string arrow, HttpContent content, CancellationToken ct)
    {
        try
        {
            int limit = _opts.MaxBodyLogLength;
            long? contentLength = content.Headers.ContentLength;

            if (limit >= 0 && contentLength > ((long) limit * 4) + 4)
            {
                _logger.Log(_opts.LogLevel, "{Arrow} Body: (not buffered; {Length} bytes exceeds the logging limit)", arrow, contentLength);
                return;
            }

            Stream stream = await content.ReadAsStreamAsync(ct).NoSync();

            // Unknown non-seekable content cannot be sampled without consuming bytes needed downstream.
            // Buffer only bodies whose declared size is within the configured logging limit.
            if (!stream.CanSeek)
            {
                if (contentLength is null || limit < 0)
                {
                    _logger.Log(_opts.LogLevel, "{Arrow} Body: (not logged; non-seekable content has no safe bounded length)", arrow);
                    return;
                }

                await content.LoadIntoBufferAsync(ct).NoSync();
                stream = await content.ReadAsStreamAsync(ct).NoSync();
            }

            stream.ToStart();

            using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, bufferSize: 4096, leaveOpen: true);
            if (limit < 0)
            {
                string completeBody = await reader.ReadToEndAsync(ct).NoSync();
                _logger.Log(_opts.LogLevel, "{Arrow} Body: {Body}", arrow, completeBody);
                stream.ToStart();
                return;
            }

            int charactersToRead = limit == int.MaxValue ? int.MaxValue : limit + 1;
            char[] rented = ArrayPool<char>.Shared.Rent(Math.Max(1, charactersToRead));
            string body;

            try
            {
                int read = await reader.ReadAsync(rented.AsMemory(0, charactersToRead), ct).NoSync();
                bool truncated = limit >= 0 && read > limit;
                int bodyLength = truncated ? limit : read;
                body = new string(rented, 0, bodyLength);
                if (truncated)
                    body += "...(truncated)";
            }
            finally
            {
                ArrayPool<char>.Shared.Return(rented);
            }

            _logger.Log(_opts.LogLevel, "{Arrow} Body: {Body}", arrow, body);

            // Rewind for downstream
            stream.ToStart();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "{Arrow} Failed to read body", arrow);
        }
    }
}
