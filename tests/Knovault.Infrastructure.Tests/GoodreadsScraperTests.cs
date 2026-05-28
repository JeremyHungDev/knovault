// tests/Knovault.Infrastructure.Tests/GoodreadsScraperTests.cs
using FluentAssertions;
using Knovault.Infrastructure.Reviews;
using Xunit;

namespace Knovault.Infrastructure.Tests;

public class GoodreadsScraperTests
{
    [Theory]
    [InlineData("https://www.goodreads.com/book/show/11468377-thinking-fast-and-slow", "11468377")]
    [InlineData("https://www.goodreads.com/en/book/show/99999-some-book", "99999")]
    public void ExtractLegacyId_returns_id_from_valid_url(string url, string expected)
    {
        GoodreadsScraper.ExtractLegacyId(url).Should().Be(expected);
    }

    [Theory]
    [InlineData("https://www.goodreads.com/search?q=test")]
    [InlineData("https://www.goodreads.com/")]
    public void ExtractLegacyId_returns_null_for_invalid_url(string url)
    {
        GoodreadsScraper.ExtractLegacyId(url).Should().BeNull();
    }

    [Fact]
    public void ExtractWorkId_finds_kca_work_id_in_html()
    {
        var html = """
            <html><head></head><body>
            <script id="__NEXT_DATA__">
            {"props":{"pageProps":{"apolloState":{"Work:kca://work/amzn1.gr.work.v1.abc123":{"id":"kca://work/amzn1.gr.work.v1.abc123"}}}}}
            </script>
            </body></html>
            """;
        GoodreadsScraper.ExtractWorkId(html).Should().Be("kca://work/amzn1.gr.work.v1.abc123");
    }

    [Fact]
    public void ExtractWorkId_returns_null_when_not_found()
    {
        GoodreadsScraper.ExtractWorkId("<html><body>no work id here</body></html>").Should().BeNull();
    }

    [Fact]
    public void ParseReviewsResponse_maps_all_fields()
    {
        var json = """
            {
              "data": {
                "getReviews": {
                  "edges": [
                    {
                      "node": {
                        "creator": { "name": "Alice" },
                        "rating": 4,
                        "text": "Great book!",
                        "createdAt": "2024-01-15T00:00:00Z",
                        "likeCount": 7
                      }
                    },
                    {
                      "node": {
                        "creator": { "name": "Bob" },
                        "rating": null,
                        "text": null,
                        "createdAt": "2023-11-02T00:00:00Z",
                        "likeCount": 0
                      }
                    }
                  ]
                }
              }
            }
            """;

        var reviews = GoodreadsScraper.ParseReviewsResponse(json);

        reviews.Should().HaveCount(2);
        reviews[0].ReviewerName.Should().Be("Alice");
        reviews[0].Rating.Should().Be(4f);
        reviews[0].ReviewText.Should().Be("Great book!");
        reviews[0].ReviewDate.Should().Be("2024-01-15T00:00:00Z");
        reviews[0].HelpfulCount.Should().Be(7);
        reviews[1].ReviewerName.Should().Be("Bob");
        reviews[1].Rating.Should().BeNull();
        reviews[1].ReviewText.Should().BeNull();
    }

    [Fact]
    public void ParseReviewsResponse_returns_empty_on_missing_data()
    {
        var json = """{"data":{"getReviews":{"edges":[]}}}""";
        GoodreadsScraper.ParseReviewsResponse(json).Should().BeEmpty();
    }
}
