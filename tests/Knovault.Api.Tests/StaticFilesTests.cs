using System.Net;
using FluentAssertions;
using Xunit;

namespace Knovault.Api.Tests;

public class StaticFilesTests : IClassFixture<TestApiFactory>
{
    private readonly TestApiFactory _factory;
    public StaticFilesTests(TestApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Root_serves_spa_index()
    {
        var client = _factory.CreateClient();
        var resp = await client.GetAsync("/");
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        (await resp.Content.ReadAsStringAsync()).Should().Contain("Knovault");
    }

    [Fact]
    public async Task Unknown_spa_route_falls_back_to_index()
    {
        var client = _factory.CreateClient();
        var resp = await client.GetAsync("/books/123");
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        (await resp.Content.ReadAsStringAsync()).Should().Contain("Knovault");
    }
}
