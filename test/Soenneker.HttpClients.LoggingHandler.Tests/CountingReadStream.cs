using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Soenneker.HttpClients.LoggingHandler.Tests;

internal sealed class CountingReadStream(byte[] bytes) : MemoryStream(bytes)
{
    public int BytesRead { get; private set; }
    public override bool CanSeek => false;

    public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        // Force short reads, including splitting multibyte UTF-8 characters.
        int read = base.Read(buffer.Span[..Math.Min(buffer.Length, 3)]);
        BytesRead += read;
        return ValueTask.FromResult(read);
    }
}
