namespace Knovault.Api.Contracts;

public sealed record UpdateCopyRequest
{
    public string? Location { get; init; }
    public string? Notes { get; init; }
}
