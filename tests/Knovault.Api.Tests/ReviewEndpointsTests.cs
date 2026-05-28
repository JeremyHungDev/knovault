// tests/Knovault.Api.Tests/ReviewEndpointsTests.cs
using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Knovault.Api.Contracts;
using Knovault.Application.Reviews;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

namespace Knovault.Api.Tests;

public class ReviewEndpointsTests : IClassFixture<TestApiFactory>
{
    private readonly TestApiFactory _factory;
    public ReviewEndpointsTests(TestApiFactory factory) => _factory = factory;

    private HttpClient ClientWithFakeService(FakeReviewService svc) =>
        _factory.CreateClientWith(services =>
        {
            services.RemoveAll<IExternalReviewService>();
            services.AddSingleton<IExternalReviewService>(svc);
        });

    [Fact]
    public async Task Get_reviews_returns_404_for_unknown_book()
    {
        var client = _factory.CreateClient();
        var resp = await client.GetAsync($"/api/books/{Guid.NewGuid()}/reviews");
        resp.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Get_reviews_returns_empty_when_book_has_no_isbn()
    {
        var client = ClientWithFakeService(new FakeReviewService());

        var createResp = await client.PostAsJsonAsync("/api/books",
            new CreatePhysicalBookRequest { Title = "No ISBN Book", Authors = new() { "Author" } });
        var book = await createResp.Content.ReadFromJsonAsync<BookDetailDto>();

        var resp = await client.GetAsync($"/api/books/{book!.Id}/reviews");
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await resp.Content.ReadFromJsonAsync<ReviewsResultDto>();
        result!.Sources.Should().BeEmpty();
    }

    [Fact]
    public async Task Get_reviews_returns_reviews_from_service()
    {
        var svc = new FakeReviewService(
            new SourceResultDto("Goodreads", DateTimeOffset.UtcNow,
                new List<ReviewDto> { new("Alice", 4f, "Good book", "2024-01-01", 5) }));

        var client = ClientWithFakeService(svc);

        var createResp = await client.PostAsJsonAsync("/api/books",
            new CreatePhysicalBookRequest { Title = "ISBN Book", Authors = new() { "Author" }, Isbn = "9780374275631" });
        var book = await createResp.Content.ReadFromJsonAsync<BookDetailDto>();

        var resp = await client.GetAsync($"/api/books/{book!.Id}/reviews");
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await resp.Content.ReadFromJsonAsync<ReviewsResultDto>();
        result!.Sources.Should().HaveCount(1);
        result.Sources[0].Reviews[0].ReviewerName.Should().Be("Alice");
    }

    [Fact]
    public async Task Post_refresh_returns_404_for_unknown_book()
    {
        var client = _factory.CreateClient();
        var resp = await client.PostAsync($"/api/books/{Guid.NewGuid()}/reviews/refresh", null);
        resp.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    internal class FakeReviewService : IExternalReviewService
    {
        private readonly SourceResultDto[] _sources;
        public FakeReviewService(params SourceResultDto[] sources) => _sources = sources;

        public Task<ReviewsResult> GetReviewsAsync(Guid bookId, string? isbn, CancellationToken ct = default)
            => Task.FromResult(Build(isbn));

        public Task<ReviewsResult> RefreshReviewsAsync(Guid bookId, string? isbn, CancellationToken ct = default)
            => Task.FromResult(Build(isbn));

        private ReviewsResult Build(string? isbn) =>
            string.IsNullOrWhiteSpace(isbn)
                ? new ReviewsResult(Array.Empty<SourceResult>())
                : new ReviewsResult(_sources.Select(s => new SourceResult(
                    Enum.Parse<Knovault.Domain.Enums.ReviewSource>(s.Source),
                    s.FetchedAt,
                    s.Reviews.Select(r => new ScrapedReview(r.ReviewerName, r.Rating, r.ReviewText, r.ReviewDate, r.HelpfulCount))
                             .ToList()
                )).ToList());
    }
}
