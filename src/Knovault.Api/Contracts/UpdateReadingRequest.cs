namespace Knovault.Api.Contracts;

public sealed record UpdateReadingRequest
{
    public string ReadingStatus { get; init; } = "None";
    public int? Percent { get; init; }
    public int? CurrentPage { get; init; }
    public int? TotalPages { get; init; }
}
