// src/Knovault.Application/Reviews/ScrapedReview.cs
namespace Knovault.Application.Reviews;

public record ScrapedReview(
    string? ReviewerName,
    float? Rating,
    string? ReviewText,
    string? ReviewDate,
    int? HelpfulCount);
