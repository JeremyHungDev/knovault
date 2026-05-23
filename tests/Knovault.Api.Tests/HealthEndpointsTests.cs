using System.Net;
using FluentAssertions;
using Xunit;

namespace Knovault.Api.Tests;

public class HealthEndpointsTests : IClassFixture<TestApiFactory>
{
    private readonly TestApiFactory _factory;
    public HealthEndpointsTests(TestApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Health_returns_ok()
    {
        var client = _factory.CreateClient();
        var resp = await client.GetAsync("/api/health");
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        (await resp.Content.ReadAsStringAsync()).Should().Contain("ok");
    }
}
