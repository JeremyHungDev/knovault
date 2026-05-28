// src/Knovault.Api/Endpoints/ReviewEndpoints.cs
using Knovault.Api.Contracts;
using Knovault.Application.Reviews;
using Knovault.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Knovault.Api.Endpoints;

public static class ReviewEndpoints
{
    public static void MapReviewEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/books/{id:guid}/reviews");
        group.MapGet("", GetReviews);
        group.MapPost("refresh", RefreshReviews);
    }

    private static async Task<IResult> GetReviews(
        KnovaultDbContext db,
        IExternalReviewService reviewService,
        Guid id,
        CancellationToken ct)
    {
        var book = await db.Books.FirstOrDefaultAsync(b => b.Id == id, ct);
        if (book is null) return Results.NotFound();

        var result = await reviewService.GetReviewsAsync(id, book.Isbn, ct);
        return Results.Ok(ToDto(result));
    }

    private static async Task<IResult> RefreshReviews(
        KnovaultDbContext db,
        IExternalReviewService reviewService,
        Guid id,
        CancellationToken ct)
    {
        var book = await db.Books.FirstOrDefaultAsync(b => b.Id == id, ct);
        if (book is null) return Results.NotFound();

        var result = await reviewService.RefreshReviewsAsync(id, book.Isbn, ct);
        return Results.Ok(ToDto(result));
    }

    private static ReviewsResultDto ToDto(ReviewsResult result) =>
        new(result.Sources.Select(s =>
            new SourceResultDto(
                s.Source.ToString(),
                s.FetchedAt,
                s.Reviews.Select(r =>
                    new ReviewDto(r.ReviewerName, r.Rating, r.ReviewText, r.ReviewDate, r.HelpfulCount))
                .ToList()))
            .ToList());
}
