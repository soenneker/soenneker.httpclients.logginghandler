using Microsoft.Extensions.Logging;
using Soenneker.Extensions.Enumerable.String;
using Soenneker.Extensions.Stream;
using Soenneker.Extensions.Task;
using Soenneker.Extensions.ValueTask;
using System;
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
        long startTimestamp = Stopwatch.GetTimestamp();
        string requestTarget = GetRequestTarget(request.RequestUri);
        _logger.Log(_opts.LogLevel, "→ {Method} {Uri}", request.Method, requestTarget);

        if (_opts.LogRequestHeaders)
        {
            LogHeaders("→", request.Headers);
            if (request.Content?.Headers != null)
                LogHeaders("→", request.Content.Headers);
        }

        if (_opts.LogRequestBody && request.Content != null)
            request.Content = await PrepareBodyLogging("→", request.Content, ct).NoSync();

        HttpResponseMessage response;

        try
        {
            response = await base.SendAsync(request, ct).NoSync();
        }
        catch (Exception ex)
        {
            long elapsedMilliseconds = (long)Stopwatch.GetElapsedTime(startTimestamp).TotalMilliseconds;
            _logger.LogError(ex, "✗ {Method} {Uri} failed after {Elapsed}ms", request.Method, requestTarget, elapsedMilliseconds);
            throw;
        }

        long responseElapsedMilliseconds = (long)Stopwatch.GetElapsedTime(startTimestamp).TotalMilliseconds;
        _logger.Log(_opts.LogLevel, "← {StatusCode} in {Elapsed}ms for {Method} {Uri}",
            response.StatusCode, responseElapsedMilliseconds, request.Method, requestTarget);

        if (_opts.LogResponseHeaders)
        {
            LogHeaders("←", response.Headers);

            if (response.Content?.Headers != null)
                LogHeaders("←", response.Content.Headers);
        }

        if (_opts.LogResponseBody && response.Content != null)
        {
            try
            {
                response.Content = await PrepareBodyLogging("←", response.Content, ct).NoSync();
            }
            catch
            {
                response.Dispose();
                throw;
            }
        }

        return response;
    }

    private string GetRequestTarget(Uri? uri)
    {
        if (uri is null || _opts.LogQueryString)
            return uri?.ToString() ?? string.Empty;

        if (uri.IsAbsoluteUri)
            return uri.GetLeftPart(UriPartial.Path);

        string value = uri.ToString();
        int suffixStart = value.AsSpan().IndexOfAny('?', '#');
        return suffixStart < 0 ? value : value[..suffixStart];
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

    private async ValueTask<HttpContent> PrepareBodyLogging(string arrow, HttpContent content, CancellationToken ct)
    {
        int limit = _opts.MaxBodyLogLength;
        if (limit >= 0 && limit != int.MaxValue)
        {
            // Observe only bytes that the transport or caller actually consumes. In particular, do not
            // ask HttpContent to buffer a response just to produce a short diagnostic preview.
            return new BodyLoggingContent(content, new BodyLogCapture(_logger, _opts.LogLevel, arrow, limit));
        }

        // Unlimited logging is an explicit opt-in to buffering the complete body.
        try
        {
            await content.LoadIntoBufferAsync(ct).NoSync();
            Stream stream = await content.ReadAsStreamAsync(ct).NoSync();
            stream.ToStart();
            try
            {
                using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, leaveOpen: true);
                string body = await reader.ReadToEndAsync(ct).NoSync();
                _logger.Log(_opts.LogLevel, "{Arrow} Body: {Body}", arrow, body);
            }
            finally
            {
                stream.ToStart();
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "{Arrow} Failed to read body", arrow);
        }

        return content;
    }
}
