namespace Knovault.Application.Parsing;

public sealed record ParsedBookMetadata
{
    public string? Title { get; init; }
    public IReadOnlyList<string> Authors { get; init; } = Array.Empty<string>();
    public string? Language { get; init; }
    public string? Publisher { get; init; }
    public string? PublishedDate { get; init; }
    public string? Isbn { get; init; }
    public string? Description { get; init; }
    public byte[]? CoverImage { get; init; }
    public string? CoverContentType { get; init; }
    public IReadOnlyList<TocEntry> Toc { get; init; } = Array.Empty<TocEntry>();
    public int? PageCount { get; init; }
}
