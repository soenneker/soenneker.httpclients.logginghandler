using AwesomeAssertions;
using Microsoft.Extensions.Logging;
using Soenneker.Tests.HostedUnit;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System;
using System.Collections.Generic;

namespace Soenneker.HttpClients.LoggingHandler.Tests;

[ClassDataSource<Host>(Shared = SharedType.PerTestSession)]
public sealed class HttpClientLoggingHandlerTests : HostedUnitTest
{

    public HttpClientLoggingHandlerTests(Host host) : base(host)
    {
    }

    [Test]
    public void Default()
    {

    }

    [Test]
    public async ValueTask Response_body_logging_does_not_consume_the_downstream_stream(CancellationToken cancellationToken)
    {
        var logger = new EnabledLogger();
        var loggingHandler = new HttpClientLoggingHandler(logger, new HttpClientLoggingOptions
        {
            LogLevel = LogLevel.Debug,
            LogResponseBody = true
        })
        {
            InnerHandler = new StreamingResponseHandler()
        };

        using var client = new HttpClient(loggingHandler);
        using HttpResponseMessage response = await client.GetAsync("https://example.test", cancellationToken: cancellationToken);
        string body = await response.Content.ReadAsStringAsync(cancellationToken: cancellationToken);

        body.Should().Be("response body");
        logger.Exception.Should().BeNull();
    }

    [Test]
    public async ValueTask Unlimited_request_body_logging_does_not_rent_a_maximum_length_array(CancellationToken cancellationToken)
    {
        var logger = new EnabledLogger();
        var innerHandler = new RequestBodyCapturingHandler();
        var loggingHandler = new HttpClientLoggingHandler(logger, new HttpClientLoggingOptions
        {
            LogLevel = LogLevel.Debug,
            LogRequestBody = true,
            LogResponseBody = false,
            MaxBodyLogLength = int.MaxValue
        })
        {
            InnerHandler = innerHandler
        };

        using var client = new HttpClient(loggingHandler);
        using var content = new StringContent("request body", Encoding.UTF8, "text/plain");
        using HttpResponseMessage response = await client.PostAsync("https://example.test", content, cancellationToken: cancellationToken);

        innerHandler.Body.Should().Be("request body");
        logger.Exception.Should().BeNull();
    }

    [Test]
    public async ValueTask Defaults_hide_sensitive_headers_queries_and_bodies(CancellationToken cancellationToken)
    {
        var logger = new EnabledLogger();
        var loggingHandler = new HttpClientLoggingHandler(logger, new HttpClientLoggingOptions())
        {
            InnerHandler = new RequestBodyCapturingHandler()
        };

        using var client = new HttpClient(loggingHandler);
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", "header-secret");
        using var content = new StringContent("body-secret", Encoding.UTF8, "text/plain");
        using HttpResponseMessage response = await client.PostAsync("https://example.test/path?token=query-secret", content, cancellationToken: cancellationToken);

        logger.Messages.Should().NotContain(message => message.Contains("header-secret", StringComparison.Ordinal));
        logger.Messages.Should().NotContain(message => message.Contains("query-secret", StringComparison.Ordinal));
        logger.Messages.Should().NotContain(message => message.Contains("body-secret", StringComparison.Ordinal));
    }

    [Test]
    [Arguments(null)]
    [Arguments(16L)]
    [Arguments(1048576L)]
    public async ValueTask Limited_logging_does_not_buffer_large_responses(long? declaredLength, CancellationToken cancellationToken)
    {
        var logger = new EnabledLogger();
        var content = new LargeStreamingContent(declaredLength);
        using var client = new HttpClient(new HttpClientLoggingHandler(logger, new HttpClientLoggingOptions
        {
            LogResponseBody = true,
            MaxBodyLogLength = 16
        }) { InnerHandler = new ContentResponseHandler(content) });

        using (HttpResponseMessage response = await client.GetAsync("https://example.test", HttpCompletionOption.ResponseHeadersRead, cancellationToken))
        {
            content.BytesWritten.Should().Be(0);
            response.Content.Headers.ContentLength.Should().Be(declaredLength);
            // Copying to a sink exercises serialization without introducing caller-side buffering.
            await response.Content.CopyToAsync(Stream.Null, cancellationToken);
            content.BytesWritten.Should().Be(LargeStreamingContent.BodyLength);
            logger.Messages.Should().Contain(message => message == "← Body: xxxxxxxxxxxxxxxx...(truncated)");
            logger.Exception.Should().BeNull();
        }
        content.Disposed.Should().BeTrue();
    }

    [Test]
    public async ValueTask Limited_request_logging_preserves_the_complete_body(CancellationToken cancellationToken)
    {
        var logger = new EnabledLogger();
        var innerHandler = new RequestBodyCapturingHandler();
        using var client = new HttpClient(new HttpClientLoggingHandler(logger, new HttpClientLoggingOptions
        {
            LogRequestBody = true,
            MaxBodyLogLength = 16
        }) { InnerHandler = innerHandler });
        using var content = new LargeStreamingContent(null);
        using HttpResponseMessage response = await client.PostAsync("https://example.test", content, cancellationToken);
        innerHandler.Body.Should().Be(new string('x', LargeStreamingContent.BodyLength));
        logger.Messages.Should().Contain(message => message == "→ Body: xxxxxxxxxxxxxxxx...(truncated)");
        logger.Exception.Should().BeNull();
    }
    [Test]
    [Arguments(0)]
    [Arguments(16)]
    public async ValueTask Stream_preview_preserves_short_reads_and_unicode(int limit, CancellationToken cancellationToken)
    {
        var logger = new EnabledLogger();
        string body = new string('é', 1024 * 1024);
        using var source = new CountingReadStream(Encoding.UTF8.GetBytes(body));
        using var client = new HttpClient(new HttpClientLoggingHandler(logger, new HttpClientLoggingOptions
        {
            LogResponseBody = true,
            MaxBodyLogLength = limit
        }) { InnerHandler = new ContentResponseHandler(new StreamContent(source)) });
        using HttpResponseMessage response = await client.GetAsync("https://example.test", HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        using Stream stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        source.BytesRead.Should().Be(0);
        using var reader = new StreamReader(stream);
        (await reader.ReadToEndAsync(cancellationToken)).Should().Be(body);
        logger.Messages.Should().Contain(message => message == $"← Body: {new string('é', limit)}...(truncated)");
        logger.Exception.Should().BeNull();
    }
    private sealed class StreamingResponseHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            byte[] bytes = Encoding.UTF8.GetBytes("response body");
            var stream = new NonSeekableStream(new MemoryStream(bytes));
            var content = new StreamContent(stream);
            content.Headers.ContentLength = bytes.Length;

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = content
            });
        }
    }

    private sealed class RequestBodyCapturingHandler : HttpMessageHandler
    {
        public string? Body { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Body = await request.Content!.ReadAsStringAsync(cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.OK);
        }
    }

    private sealed class NonSeekableStream : Stream
    {
        private readonly Stream _inner;

        public NonSeekableStream(Stream inner) => _inner = inner;
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new System.NotSupportedException();
        public override long Position { get => throw new System.NotSupportedException(); set => throw new System.NotSupportedException(); }
        public override void Flush() => _inner.Flush();
        public override int Read(byte[] buffer, int offset, int count) => _inner.Read(buffer, offset, count);
        public override long Seek(long offset, SeekOrigin origin) => throw new System.NotSupportedException();
        public override void SetLength(long value) => throw new System.NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new System.NotSupportedException();
        protected override void Dispose(bool disposing) { if (disposing) _inner.Dispose(); base.Dispose(disposing); }
    }

    private sealed class EnabledLogger : ILogger
    {
        public System.Exception? Exception { get; private set; }
        public List<string> Messages { get; } = [];

        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, System.Exception? exception,
            System.Func<TState, System.Exception?, string> formatter)
        {
            Exception ??= exception;
            Messages.Add(formatter(state, exception));
        }
        public System.IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
    }
}
