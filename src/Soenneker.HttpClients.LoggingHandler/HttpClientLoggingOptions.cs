using Microsoft.Extensions.Logging;
using System.Collections.Generic;

namespace Soenneker.HttpClients.LoggingHandler;

/// <summary>
/// Options for <see cref="HttpClientLoggingHandler"/>.
/// </summary>
public sealed class HttpClientLoggingOptions
{
    /// <summary>Maximum number of characters to log from a body. A negative value or <see cref="int.MaxValue"/> removes the limit.</summary>
    public int MaxBodyLogLength { get; set; } = 4096;

    /// <summary>Header names whose values are replaced with <c>***</c>.</summary>
    public List<string>? RedactedHeaders { get; set; } =
    [
        "Authorization",
        "Proxy-Authorization",
        "Cookie",
        "Set-Cookie",
        "X-Api-Key",
        "Api-Key"
    ];

    /// <summary>
    /// Gets or sets a value indicating whether log request body.
    /// </summary>
    public bool LogRequestBody { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether log response body.
    /// </summary>
    public bool LogResponseBody { get; set; }

    /// <summary>
    /// Gets or sets whether request query strings are included in log messages.
    /// </summary>
    public bool LogQueryString { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether log request headers.
    /// </summary>
    public bool LogRequestHeaders { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether log response headers.
    /// </summary>
    public bool LogResponseHeaders { get; set; } = true;

    /// <summary>Minimum level for logging headers and status.</summary>
    public LogLevel LogLevel { get; set; } = LogLevel.Information;
}
