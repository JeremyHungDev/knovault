namespace Knovault.Api.Contracts;

public sealed record CreateTagRequest
{
    public string Name { get; init; } = "";
    public string? Color { get; init; }
}
