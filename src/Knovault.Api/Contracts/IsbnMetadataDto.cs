namespace Knovault.Api.Contracts;

public sealed record IsbnMetadataDto
{
    public string? Title { get; init; }
    public IReadOnlyList<string> Authors { get; init; } = Array.Empty<string>();
    public string? Publisher { get; init; }
    public string? PublishedDate { get; init; }
    public string? Isbn { get; init; }
    public int? PageCount { get; init; }
}
