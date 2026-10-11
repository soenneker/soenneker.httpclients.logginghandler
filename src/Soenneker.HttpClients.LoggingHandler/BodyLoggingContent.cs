using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Soenneker.HttpClients.LoggingHandler;

internal sealed class BodyLoggingContent : HttpContent
{
    private readonly HttpContent _inner;
    private readonly BodyLogCapture _capture;

    public BodyLoggingContent(HttpContent inner, BodyLogCapture capture)
    {
        _inner = inner;
        _capture = capture;
        foreach (var header in inner.Headers)
            Headers.TryAddWithoutValidation(header.Key, header.Value);
    }

    protected override bool TryComputeLength(out long length)
    {
        length = _inner.Headers.ContentLength.GetValueOrDefault();
        return _inner.Headers.ContentLength.HasValue;
    }

    protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context) =>
        SerializeToStreamAsync(stream, context, CancellationToken.None);

    protected override async Task SerializeToStreamAsync(Stream stream, TransportContext? context, CancellationToken cancellationToken)
    {
        using var loggingStream = new BodyLoggingStream(stream, _capture, leaveOpen: true);
        await _inner.CopyToAsync(loggingStream, context, cancellationToken).ConfigureAwait(false);
        _capture.Complete();
    }

    protected override Stream CreateContentReadStream(CancellationToken cancellationToken) =>
        new BodyLoggingStream(_inner.ReadAsStream(cancellationToken), _capture);

    protected override Task<Stream> CreateContentReadStreamAsync() => CreateContentReadStreamAsync(CancellationToken.None);

    protected override async Task<Stream> CreateContentReadStreamAsync(CancellationToken cancellationToken) =>
        new BodyLoggingStream(await _inner.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false), _capture);

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _capture.Complete();
            _inner.Dispose();
        }
        base.Dispose(disposing);
    }
}
