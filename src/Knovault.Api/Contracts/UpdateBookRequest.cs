namespace Knovault.Api.Contracts;

public sealed record UpdateBookRequest
{
    public string Title { get; init; } = "";
    public string? Subtitle { get; init; }
    public List<string> Authors { get; init; } = new();
    public string? Language { get; init; }
    public string? Publisher { get; init; }
    public string? PublishedDate { get; init; }
    public string? Description { get; init; }
    public string? Isbn { get; init; }
    public bool IsPhysical { get; init; }
}
