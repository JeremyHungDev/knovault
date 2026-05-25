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
    public bool HasDigital { get; init; }
    public bool IsPhysical { get; init; }
    public string? PhysicalLocation { get; init; }
    public string? PhysicalNotes { get; init; }
    public IReadOnlyList<string> Tags { get; init; } = Array.Empty<string>();
    public IReadOnlyList<CopyDto> Copies { get; init; } = Array.Empty<CopyDto>();
}
