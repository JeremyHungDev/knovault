using System.Net;
using FluentAssertions;
using Xunit;

namespace Knovault.Api.Tests;

public class ScanStreamTests : IClassFixture<TestApiFactory>
{
    private readonly TestApiFactory _factory;
    public ScanStreamTests(TestApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Scan_stream_emits_done_event()
    {
        var client = _factory.CreateClient();
        var resp = await client.GetAsync("/api/library/scan/stream");
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await resp.Content.ReadAsStringAsync();
        body.Should().Contain("event: done");
    }
}
