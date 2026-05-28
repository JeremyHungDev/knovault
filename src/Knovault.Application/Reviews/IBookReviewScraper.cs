// src/Knovault.Application/Reviews/IBookReviewScraper.cs
using Knovault.Domain.Enums;

namespace Knovault.Application.Reviews;

public interface IBookReviewScraper
{
    ReviewSource Source { get; }
    Task<IReadOnlyList<ScrapedReview>> ScrapeAsync(string isbn, CancellationToken ct = default);
}
