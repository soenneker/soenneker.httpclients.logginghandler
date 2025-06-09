using Microsoft.Extensions.Logging;
using System.Collections.Generic;

namespace Soenneker.HttpClients.LoggingHandler;

/// <summary>
/// Options for <see cref="HttpClientLoggingHandler"/>.
/// </summary>
public sealed class HttpClientLoggingOptions
{
    /// <summary>Max number of characters to read from a body. 
    /// Use a negative or int.MaxValue for “unlimited.”</summary>
    public int MaxBodyLogLength { get; set; } = int.MaxValue;

    /// <summary>Headers to redact (e.g. Authorization).</summary>
    public List<string>? RedactedHeaders { get; set; }

    public bool LogRequestBody { get; set; } = true;

    public bool LogResponseBody { get; set; } = true;

    public bool LogRequestHeaders { get; set; } = true;

    public bool LogResponseHeaders { get; set; } = true;

    /// <summary>Minimum level for logging headers and status.</summary>
    public LogLevel LogLevel { get; set; } = LogLevel.Information;
}