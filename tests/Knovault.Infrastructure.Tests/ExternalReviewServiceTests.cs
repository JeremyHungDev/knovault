// tests/Knovault.Infrastructure.Tests/ExternalReviewServiceTests.cs
using FluentAssertions;
using Knovault.Application.Reviews;
using Knovault.Domain.Entities;
using Knovault.Domain.Enums;
using Knovault.Infrastructure.Reviews;
using Xunit;

namespace Knovault.Infrastructure.Tests;

public class ExternalReviewServiceTests
{
    private static Book MakeBook(string? isbn = "9780374275631")
    {
        var b = new Book("Test Book");
        b.UpdateMetadata("Test Book", null, null, null, null, null, isbn);
        return b;
    }

    [Fact]
    public async Task GetReviewsAsync_returns_empty_when_isbn_is_null()
    {
        using var db = new SqliteTestDb();
        await using var ctx = db.NewContext();
        var svc = new ExternalReviewService(ctx, Array.Empty<IBookReviewScraper>());

        var result = await svc.GetReviewsAsync(Guid.NewGuid(), null);

        result.Sources.Should().BeEmpty();
    }

    [Fact]
    public async Task GetReviewsAsync_calls_scraper_when_no_cache_exists()
    {
        using var db = new SqliteTestDb();
        var bookId = Guid.NewGuid();
        var scraper = new FakeScraper(ReviewSource.Goodreads,
            new ScrapedReview("Alice", 4f, "Good", "2024-01-01", 3));

        await using var ctx = db.NewContext();
        var svc = new ExternalReviewService(ctx, new[] { scraper });

        var result = await svc.GetReviewsAsync(bookId, "9780374275631");

        result.Sources.Should().HaveCount(1);
        result.Sources[0].Source.Should().Be(ReviewSource.Goodreads);
        result.Sources[0].Reviews.Should().HaveCount(1);
        result.Sources[0].Reviews[0].ReviewerName.Should().Be("Alice");
        scraper.CallCount.Should().Be(1);
    }

    [Fact]
    public async Task GetReviewsAsync_returns_cache_without_calling_scraper()
    {
        using var db = new SqliteTestDb();
        var bookId = Guid.NewGuid();
        var fetchedAt = DateTimeOffset.UtcNow.AddHours(-1);

        await using (var ctx = db.NewContext())
        {
            ctx.ExternalReviews.Add(new ExternalReview(
                bookId, ReviewSource.Goodreads, "Bob", 3f, "Okay", "2023-06-01", 0, fetchedAt));
            await ctx.SaveChangesAsync();
        }

        var scraper = new FakeScraper(ReviewSource.Goodreads);
        await using var ctx2 = db.NewContext();
        var svc = new ExternalReviewService(ctx2, new[] { scraper });

        var result = await svc.GetReviewsAsync(bookId, "9780374275631");

        scraper.CallCount.Should().Be(0);
        result.Sources[0].Reviews[0].ReviewerName.Should().Be("Bob");
    }

    [Fact]
    public async Task RefreshReviewsAsync_overwrites_existing_cache()
    {
        using var db = new SqliteTestDb();
        var bookId = Guid.NewGuid();

        await using (var ctx = db.NewContext())
        {
            ctx.ExternalReviews.Add(new ExternalReview(
                bookId, ReviewSource.Goodreads, "OldReviewer", 2f, "Old", "2020-01-01", 0, DateTimeOffset.UtcNow));
            await ctx.SaveChangesAsync();
        }

        var scraper = new FakeScraper(ReviewSource.Goodreads,
            new ScrapedReview("NewReviewer", 5f, "Excellent", "2024-12-01", 20));

        await using var ctx2 = db.NewContext();
        var svc = new ExternalReviewService(ctx2, new[] { scraper });

        var result = await svc.RefreshReviewsAsync(bookId, "9780374275631");

        result.Sources[0].Reviews.Should().HaveCount(1);
        result.Sources[0].Reviews[0].ReviewerName.Should().Be("NewReviewer");
    }

    private class FakeScraper : IBookReviewScraper
    {
        private readonly ScrapedReview[] _reviews;
        public int CallCount { get; private set; }
        public ReviewSource Source { get; }

        public FakeScraper(ReviewSource source, params ScrapedReview[] reviews)
        {
            Source = source;
            _reviews = reviews;
        }

        public Task<IReadOnlyList<ScrapedReview>> ScrapeAsync(string isbn, CancellationToken ct = default)
        {
            CallCount++;
            return Task.FromResult<IReadOnlyList<ScrapedReview>>(_reviews);
        }
    }
}
