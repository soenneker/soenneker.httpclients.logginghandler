using System;
using System.Net;
using System.Net.Http;
using Microsoft.Extensions.Logging;

namespace Soenneker.HttpClients.LoggingHandler;

internal static partial class HttpLog
{
    [LoggerMessage(EventId = 1, Message = "-> {Method} {Uri}")]
    public static partial void Request(ILogger logger, LogLevel level, HttpMethod method, string uri);

    [LoggerMessage(EventId = 2, Message = "<- {StatusCode} in {Elapsed}ms for {Method} {Uri}")]
    public static partial void Response(ILogger logger, LogLevel level, HttpStatusCode statusCode, long elapsed, HttpMethod method, string uri);

    [LoggerMessage(EventId = 3, Level = LogLevel.Error, Message = "! {Method} {Uri} failed after {Elapsed}ms")]
    public static partial void Failure(ILogger logger, Exception exception, HttpMethod method, string uri, long elapsed);

    [LoggerMessage(EventId = 4, Message = "{Arrow} Header {Key}: {Value}")]
    public static partial void Header(ILogger logger, LogLevel level, string arrow, string key, string value);

    [LoggerMessage(EventId = 5, Message = "{Arrow} Body: {Body}")]
    public static partial void Body(ILogger logger, LogLevel level, string arrow, string body);

    [LoggerMessage(EventId = 6, Message = "{Arrow} Body omitted (Content-Type: {ContentType}, Content-Length: {ContentLength})")]
    public static partial void Omitted(ILogger logger, LogLevel level, string arrow, string contentType, long? contentLength);

    [LoggerMessage(EventId = 7, Level = LogLevel.Warning, Message = "{Arrow} Failed to read body")]
    public static partial void BodyFailure(ILogger logger, Exception exception, string arrow);
}
