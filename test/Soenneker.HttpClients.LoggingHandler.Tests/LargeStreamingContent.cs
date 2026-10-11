using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;

namespace Soenneker.HttpClients.LoggingHandler.Tests;

internal sealed class LargeStreamingContent(long? declaredLength) : HttpContent
{
    public const int BodyLength = 1024 * 1024;
    public int BytesWritten { get; private set; }
    public bool Disposed { get; private set; }

    protected override bool TryComputeLength(out long length)
    {
        length = declaredLength.GetValueOrDefault();
        return declaredLength.HasValue;
    }

    protected override async Task SerializeToStreamAsync(Stream stream, TransportContext? context)
    {
        byte[] chunk = new byte[4096];
        Array.Fill(chunk, (byte) 'x');
        for (int i = 0; i < BodyLength; i += chunk.Length)
        {
            await stream.WriteAsync(chunk);
            BytesWritten += chunk.Length;
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            Disposed = true;
        base.Dispose(disposing);
    }
}
