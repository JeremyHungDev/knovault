namespace Knovault.Api.Contracts;

public sealed record UpdateReadingRequest
{
    public string ReadingStatus { get; init; } = "None";
}
