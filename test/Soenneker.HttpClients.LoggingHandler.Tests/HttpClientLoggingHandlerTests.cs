using Soenneker.Tests.HostedUnit;

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
}
