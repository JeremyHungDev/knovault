namespace Knovault.Api.Contracts;

public sealed record UpdatePhysicalRequest
{
    public bool IsPhysical { get; init; }
    public string? Location { get; init; }
    public string? Notes { get; init; }
}
