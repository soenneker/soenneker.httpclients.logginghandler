using AwesomeAssertions;
using Microsoft.Extensions.Logging;
using Soenneker.Tests.HostedUnit;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

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
    public async Task Response_body_logging_does_not_consume_the_downstream_stream()
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
        using HttpResponseMessage response = await client.GetAsync("https://example.test");
        string body = await response.Content.ReadAsStringAsync();

        body.Should().Be("response body");
        logger.Exception.Should().BeNull();
    }

    [Test]
    public async Task Unlimited_request_body_logging_does_not_rent_a_maximum_length_array()
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
        using HttpResponseMessage response = await client.PostAsync("https://example.test", content);

        innerHandler.Body.Should().Be("request body");
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

        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, System.Exception? exception,
            System.Func<TState, System.Exception?, string> formatter)
        {
            Exception ??= exception;
        }
        public System.IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
    }
}
