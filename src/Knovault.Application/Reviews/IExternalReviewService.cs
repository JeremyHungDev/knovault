// src/Knovault.Application/Reviews/IExternalReviewService.cs
using Knovault.Domain.Enums;

namespace Knovault.Application.Reviews;

public interface IExternalReviewService
{
    Task<ReviewsResult> GetReviewsAsync(Guid bookId, string? isbn, CancellationToken ct = default);
    Task<ReviewsResult> RefreshReviewsAsync(Guid bookId, string? isbn, CancellationToken ct = default);
}

public record ReviewsResult(IReadOnlyList<SourceResult> Sources);

public record SourceResult(
    ReviewSource Source,
    DateTimeOffset? FetchedAt,
    IReadOnlyList<ScrapedReview> Reviews);
