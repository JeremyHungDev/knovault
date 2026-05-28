// src/Knovault.Infrastructure/Reviews/ExternalReviewService.cs
using Knovault.Application.Reviews;
using Knovault.Domain.Entities;
using Knovault.Domain.Enums;
using Knovault.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Knovault.Infrastructure.Reviews;

public class ExternalReviewService : IExternalReviewService
{
    private readonly KnovaultDbContext _db;
    private readonly IEnumerable<IBookReviewScraper> _scrapers;

    public ExternalReviewService(KnovaultDbContext db, IEnumerable<IBookReviewScraper> scrapers)
    {
        _db = db;
        _scrapers = scrapers;
    }

    public async Task<ReviewsResult> GetReviewsAsync(Guid bookId, string? isbn, CancellationToken ct = default)
    {
        var cached = await _db.ExternalReviews
            .Where(r => r.BookId == bookId)
            .ToListAsync(ct);

        if (cached.Count > 0)
            return ToResult(cached);

        if (string.IsNullOrWhiteSpace(isbn))
            return new ReviewsResult(Array.Empty<SourceResult>());

        return await ScrapeAndCacheAsync(bookId, isbn, ct);
    }

    public async Task<ReviewsResult> RefreshReviewsAsync(Guid bookId, string? isbn, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(isbn))
            return new ReviewsResult(Array.Empty<SourceResult>());

        return await ScrapeAndCacheAsync(bookId, isbn, ct);
    }

    private async Task<ReviewsResult> ScrapeAndCacheAsync(Guid bookId, string isbn, CancellationToken ct)
    {
        var fetchedAt = DateTimeOffset.UtcNow;

        foreach (var scraper in _scrapers)
        {
            var existing = await _db.ExternalReviews
                .Where(r => r.BookId == bookId && r.Source == scraper.Source)
                .ToListAsync(ct);
            _db.ExternalReviews.RemoveRange(existing);

            var scraped = await scraper.ScrapeAsync(isbn, ct);
            foreach (var r in scraped)
                _db.ExternalReviews.Add(new ExternalReview(
                    bookId, scraper.Source,
                    r.ReviewerName, r.Rating, r.ReviewText, r.ReviewDate, r.HelpfulCount,
                    fetchedAt));
        }

        await _db.SaveChangesAsync(ct);

        var all = await _db.ExternalReviews
            .Where(r => r.BookId == bookId)
            .ToListAsync(ct);
        return ToResult(all);
    }

    private static ReviewsResult ToResult(IList<ExternalReview> reviews)
    {
        var groups = reviews
            .GroupBy(r => r.Source)
            .Select(g => new SourceResult(
                g.Key,
                g.Max(r => r.FetchedAt),
                g.Select(r => new ScrapedReview(r.ReviewerName, r.Rating, r.ReviewText, r.ReviewDate, r.HelpfulCount))
                 .ToList()))
            .ToList();
        return new ReviewsResult(groups);
    }
}
