namespace Knovault.Application.Parsing;

public sealed class TocEntry
{
    public string Title { get; init; } = "";
    public string? Href { get; init; }
    public List<TocEntry> Children { get; init; } = new();
}
