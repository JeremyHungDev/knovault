using Knovault.Application.Reviews;
using Knovault.Domain.Enums;

namespace Knovault.Infrastructure.Reviews;

// Phase 2: 博客來 product pages are behind Cloudflare Managed Challenge.
// Requires Microsoft.Playwright (headless Chromium). Stub returns empty until implemented.
public class BooksComTwScraper : IBookReviewScraper
{
    public ReviewSource Source => ReviewSource.BooksComTw;

    public Task<IReadOnlyList<ScrapedReview>> ScrapeAsync(string isbn, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<ScrapedReview>>(Array.Empty<ScrapedReview>());
}
