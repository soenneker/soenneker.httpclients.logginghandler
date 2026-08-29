[![](https://img.shields.io/nuget/v/soenneker.httpclients.logginghandler.svg?style=for-the-badge)](https://www.nuget.org/packages/soenneker.httpclients.logginghandler/)
[![](https://img.shields.io/github/actions/workflow/status/soenneker/soenneker.httpclients.logginghandler/publish-package.yml?style=for-the-badge)](https://github.com/soenneker/soenneker.httpclients.logginghandler/actions/workflows/publish-package.yml)
[![](https://img.shields.io/nuget/dt/soenneker.httpclients.logginghandler.svg?style=for-the-badge)](https://www.nuget.org/packages/soenneker.httpclients.logginghandler/)
[![](https://img.shields.io/github/actions/workflow/status/soenneker/soenneker.httpclients.logginghandler/codeql.yml?label=CodeQL&style=for-the-badge)](https://github.com/soenneker/soenneker.httpclients.logginghandler/actions/workflows/codeql.yml)

# Soenneker.HttpClients.LoggingHandler

A delegating handler that logs HTTP request and response details, including headers and optionally bodies, for diagnostic and debugging purposes.

## Install

```bash
dotnet add package Soenneker.HttpClients.LoggingHandler
```

## What you get

- `HttpClientLoggingHandler` — A delegating handler that logs HTTP request and response details, including headers and optionally bodies, for diagnostic and debugging purposes.
- `HttpClientLoggingOptions` — Options for `HttpClientLoggingHandler`.

## API at a glance

| API | What it does | Result / important behavior |
| --- | --- | --- |
| `HttpClientLoggingOptions.MaxBodyLogLength` | Max number of characters to read from a body. Use a negative or int.MaxValue for “unlimited.”. | Max number of characters to read from a body. Use a negative or int.MaxValue for “unlimited.”. |
| `HttpClientLoggingOptions.RedactedHeaders` | Headers to redact (e.g. Authorization). | Headers to redact (e.g. Authorization). |
| `HttpClientLoggingOptions.LogRequestBody` | Gets or sets a value indicating whether log request body. | Gets or sets a value indicating whether log request body. |
| `HttpClientLoggingOptions.LogResponseBody` | Gets or sets a value indicating whether log response body. | Gets or sets a value indicating whether log response body. |
| `HttpClientLoggingOptions.LogRequestHeaders` | Gets or sets a value indicating whether log request headers. | Gets or sets a value indicating whether log request headers. |
| `HttpClientLoggingOptions.LogResponseHeaders` | Gets or sets a value indicating whether log response headers. | Gets or sets a value indicating whether log response headers. |
| `HttpClientLoggingOptions.LogLevel` | Minimum level for logging headers and status. | Minimum level for logging headers and status. |
