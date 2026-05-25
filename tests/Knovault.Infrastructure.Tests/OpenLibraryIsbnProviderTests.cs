using System.Net;
using System.Text;
using FluentAssertions;
using Knovault.Infrastructure.Metadata;
using Xunit;

namespace Knovault.Infrastructure.Tests;

public class OpenLibraryIsbnProviderTests
{
    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly string _json;
        public StubHandler(string json) => _json = json;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(_json, Encoding.UTF8, "application/json")
            });
    }

    [Fact]
    public async Task Lookup_parses_openlibrary_response()
    {
        const string isbn = "9780321125217";
        var json = $$"""
            {
              "ISBN:{{isbn}}": {
                "title": "Domain-Driven Design",
                "authors": [{ "name": "Eric Evans" }],
                "publishers": [{ "name": "Addison-Wesley" }],
                "publish_date": "2003-10-?",
                "number_of_pages": 560,
                "cover": { "small": "https://covers/s.jpg", "medium": "https://covers/m.jpg", "large": "https://covers/l.jpg" }
              }
            }
            """;
        var provider = new OpenLibraryIsbnProvider(new HttpClient(new StubHandler(json)));

        var meta = await provider.LookupAsync(isbn);

        meta.Should().NotBeNull();
        meta!.Title.Should().Be("Domain-Driven Design");
        meta.Authors.Should().ContainSingle().Which.Should().Be("Eric Evans");
        meta.Publisher.Should().Be("Addison-Wesley");
        meta.PageCount.Should().Be(560);
        meta.Isbn.Should().Be(isbn);
        meta.CoverUrl.Should().Be("https://covers/l.jpg");
        meta.PublishedDate.Should().Be("2003-10"); // "2003-10-?" 清理後
    }

    [Fact]
    public async Task Lookup_returns_null_when_not_found()
    {
        var provider = new OpenLibraryIsbnProvider(new HttpClient(new StubHandler("{}")));
        (await provider.LookupAsync("0000000000")).Should().BeNull();
    }
}
