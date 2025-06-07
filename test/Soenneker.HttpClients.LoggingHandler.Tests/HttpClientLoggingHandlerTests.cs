using Soenneker.Tests.FixturedUnit;
using Xunit;

namespace Soenneker.HttpClients.LoggingHandler.Tests;

[Collection("Collection")]
public sealed class HttpClientLoggingHandlerTests : FixturedUnitTest
{

    public HttpClientLoggingHandlerTests(Fixture fixture, ITestOutputHelper output) : base(fixture, output)
    {
    }

    [Fact]
    public void Default()
    {

    }
}
