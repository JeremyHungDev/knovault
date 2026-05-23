namespace Knovault.Api.Contracts;

public sealed record CreatePhysicalBookRequest
{
    public string Title { get; init; } = "";
    public List<string> Authors { get; init; } = new();
    public string? Isbn { get; init; }
    public string? Publisher { get; init; }
    public string? PublishedDate { get; init; }
    public string? Language { get; init; }
    public string? Description { get; init; }
    public string? Location { get; init; }
}
