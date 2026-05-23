namespace Knovault.Api.Contracts;

public sealed record AddPhysicalCopyRequest
{
    public string? Location { get; init; }
    public string? Notes { get; init; }
}
