namespace Knovault.Api.Contracts;

public sealed record BookSummaryDto
{
    public Guid Id { get; init; }
    public string Title { get; init; } = "";
    public IReadOnlyList<string> Authors { get; init; } = Array.Empty<string>();
    public string? CoverPath { get; init; }
    public string ReadingStatus { get; init; } = "";
    public bool HasDigital { get; init; }
    public bool HasPhysical { get; init; }
}
