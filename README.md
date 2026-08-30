[![](https://img.shields.io/nuget/v/soenneker.httpclients.logginghandler.svg?style=for-the-badge)](https://www.nuget.org/packages/soenneker.httpclients.logginghandler/)
[![](https://img.shields.io/github/actions/workflow/status/soenneker/soenneker.httpclients.logginghandler/build-and-test.yml?style=for-the-badge)](https://github.com/soenneker/soenneker.httpclients.logginghandler/actions/workflows/build-and-test.yml)
[![](https://img.shields.io/github/actions/workflow/status/soenneker/soenneker.httpclients.logginghandler/publish-package.yml?style=for-the-badge)](https://github.com/soenneker/soenneker.httpclients.logginghandler/actions/workflows/publish-package.yml)
[![](https://img.shields.io/nuget/dt/soenneker.httpclients.logginghandler.svg?style=for-the-badge)](https://www.nuget.org/packages/soenneker.httpclients.logginghandler/)
[![](https://img.shields.io/github/actions/workflow/status/soenneker/soenneker.httpclients.logginghandler/codeql.yml?label=CodeQL&style=for-the-badge)](https://github.com/soenneker/soenneker.httpclients.logginghandler/actions/workflows/codeql.yml)

# Soenneker.HttpClients.LoggingHandler

A delegating handler for structured HTTP request, response, timing, header, and optional body logs.

## Install

```bash
dotnet add package Soenneker.HttpClients.LoggingHandler
```

## Configure with `IHttpClientFactory`

```csharp
using Microsoft.Extensions.Logging;
using Soenneker.HttpClients.LoggingHandler;

var options = new HttpClientLoggingOptions
{
    LogLevel = LogLevel.Debug,
    LogRequestHeaders = true,
    LogResponseHeaders = true,
    LogRequestBody = false,
    LogResponseBody = false
};

services.AddHttpClient("catalog", client =>
{
    client.BaseAddress = new Uri("https://api.example.com/");
})
.AddHttpMessageHandler(serviceProvider =>
    new HttpClientLoggingHandler(
        serviceProvider.GetRequiredService<ILogger<HttpClientLoggingHandler>>(),
        options));
```

The handler logs the method, path, status code, and elapsed time. Header logging is enabled by default. Request and response bodies and query strings are disabled by default because they commonly contain credentials or personal data.

## Redaction and body limits

The default redaction list covers `Authorization`, `Proxy-Authorization`, `Cookie`, `Set-Cookie`, `X-Api-Key`, and `Api-Key`. Replace or extend `RedactedHeaders` before constructing the handler for application-specific secrets.

```csharp
var options = new HttpClientLoggingOptions
{
    LogResponseBody = true,
    MaxBodyLogLength = 8_192,
    RedactedHeaders =
    [
        "Authorization",
        "Cookie",
        "Set-Cookie",
        "X-Api-Key",
        "X-Tenant-Token"
    ]
};
```

`MaxBodyLogLength` defaults to 4,096 characters. A negative value or `int.MaxValue` removes the limit and can buffer arbitrarily large content; use that only with payload sizes you control.

Body inspection rewinds seekable content so downstream consumers can still read it. Content with no safely bounded, seekable representation is skipped. A body-read failure is logged as a warning and does not fail the HTTP call, but caller-requested cancellation still propagates.

Set `LogQueryString = true` only when query parameters are known not to contain secrets. URI query values and body fields are not selectively redacted: when enabled, they are logged as supplied.
