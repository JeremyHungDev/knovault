namespace Knovault.Api.Contracts;

public sealed record CreateFolderRequest
{
    public string Path { get; init; } = "";
    public string? DisplayName { get; init; }
}
