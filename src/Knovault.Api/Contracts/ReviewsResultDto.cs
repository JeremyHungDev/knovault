// src/Knovault.Api/Contracts/ReviewsResultDto.cs
namespace Knovault.Api.Contracts;

public record ReviewsResultDto(IReadOnlyList<SourceResultDto> Sources);

public record SourceResultDto(
    string Source,
    DateTimeOffset? FetchedAt,
    IReadOnlyList<ReviewDto> Reviews);

public record ReviewDto(
    string? ReviewerName,
    float? Rating,
    string? ReviewText,
    string? ReviewDate,
    int? HelpfulCount);
