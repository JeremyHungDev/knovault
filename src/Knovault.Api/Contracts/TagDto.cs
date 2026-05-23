namespace Knovault.Api.Contracts;

public sealed record TagDto(Guid Id, string Name, string? Color, int BookCount);
