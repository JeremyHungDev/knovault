namespace Knovault.Api.Contracts;

public sealed record CopyDto
{
    public Guid Id { get; init; }
    public string Type { get; init; } = "";        // "digital" | "physical"
    public string? Format { get; init; }            // digital
    public long? FileSizeBytes { get; init; }       // digital
    public bool? IsMissing { get; init; }           // digital
    public bool? ParseFailed { get; init; }         // digital
    public string? Location { get; init; }          // physical
}
