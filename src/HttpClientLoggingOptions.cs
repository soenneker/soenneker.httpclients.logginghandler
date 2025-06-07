using Microsoft.Extensions.Logging;
using System.Collections.Generic;

namespace Soenneker.HttpClients.LoggingHandler;

/// <summary>
/// Options for <see cref="HttpClientLoggingHandler"/>.
/// </summary>
public class HttpClientLoggingOptions
{
    /// <summary>Max number of characters to read from a body. 
    /// Use a negative or int.MaxValue for “unlimited.”</summary>
    public int MaxBodyLogLength { get; set; } = int.MaxValue;

    /// <summary>Headers to redact (e.g. Authorization).</summary>
    public List<string>? RedactedHeaders { get; set; }

    /// <summary>Whether to log request/response bodies.</summary>
    public bool LogBodies { get; set; } = true;

    /// <summary>Minimum level for logging headers and status.</summary>
    public LogLevel LogLevel { get; set; } = LogLevel.Information;
}