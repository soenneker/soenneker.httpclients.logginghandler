using Microsoft.Extensions.Logging;
using Soenneker.Extensions.Enumerable.String;
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
/// A delegating handler that logs HTTP request and response details, including headers and optionally bodies,  for
/// diagnostic and debugging purposes.
/// </summary>
/// <remarks>This handler logs HTTP requests and responses using the provided <see cref="ILogger"/> instance.  It
/// supports configurable options for logging headers, redacting sensitive information, and truncating  large request or
/// response bodies. The handler can be used to inspect HTTP traffic in applications  for debugging or monitoring
/// purposes.  To use this handler, add it to the <see cref="HttpClient"/> pipeline by wrapping it around other handlers
/// or directly assigning it to the <see cref="HttpClient"/> instance.  Thread safety: This handler is thread-safe and
/// can be used in concurrent scenarios.</remarks>
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

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();

        // Log request line
        _logger.Log(_opts.LogLevel, "→ {Method} {Uri}", request.Method, request.RequestUri);

        LogHeaders("→", request.Headers);

        if (_opts.LogBodies && request.Content != null)
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
        _logger.Log(_opts.LogLevel, "← {StatusCode} in {Elapsed}ms for {Method} {Uri}", response.StatusCode, sw.ElapsedMilliseconds, request.Method,
            request.RequestUri);

        LogHeaders("←", response.Headers);

        if (_opts.LogBodies && response.Content != null)
            await LogBody("←", response.Content, ct).NoSync();

        return response;
    }

    private void LogHeaders(string arrow, HttpHeaders headers)
    {
        foreach (KeyValuePair<string, IEnumerable<string>> header in headers)
        {
            // string.Join allocates one string per header value set
            string value;

            if (_redactions.Contains(header.Key))
                value = "***";
            else
                value = _redactions.ToCommaSeparatedString(true);

            _logger.Log(_opts.LogLevel, "{Arrow} Header {Key}: {Value}", arrow, header.Key, value);
        }
    }

    private async ValueTask LogBody(string arrow, HttpContent content, CancellationToken ct)
    {
        try
        {
            // Read stream with a 4 KiB pooled buffer
            await using Stream stream = await content.ReadAsStreamAsync(ct).NoSync();

            using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, bufferSize: 4096, leaveOpen: true);

            string body = await reader.ReadToEndAsync(ct).NoSync();

            if (_opts.MaxBodyLogLength >= 0 && body.Length > _opts.MaxBodyLogLength)
            {
                body = body.Substring(0, _opts.MaxBodyLogLength) + "...(truncated)";
            }

            _logger.Log(_opts.LogLevel, "{Arrow} Body: {Body}", arrow, body);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "{Arrow} Failed to read body", arrow);
        }
    }
}