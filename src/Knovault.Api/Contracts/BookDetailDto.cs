namespace Knovault.Api.Contracts;

public sealed record BookDetailDto
{
    public Guid Id { get; init; }
    public string Title { get; init; } = "";
    public string? Subtitle { get; init; }
    public IReadOnlyList<string> Authors { get; init; } = Array.Empty<string>();
    public string? Language { get; init; }
    public string? Publisher { get; init; }
    public string? PublishedDate { get; init; }
    public string? Description { get; init; }
    public string? Isbn { get; init; }
    public string? CoverPath { get; init; }
    public string ReadingStatus { get; init; } = "";
    public int? ProgressPercent { get; init; }
    public int? CurrentPage { get; init; }
    public int? TotalPages { get; init; }
    public IReadOnlyList<string> Tags { get; init; } = Array.Empty<string>();
    public IReadOnlyList<CopyDto> Copies { get; init; } = Array.Empty<CopyDto>();
}
