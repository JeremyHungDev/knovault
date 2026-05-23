namespace Knovault.Api.Contracts;

public sealed record FolderDto(Guid Id, string Path, string? DisplayName, bool Enabled, DateTimeOffset? LastScannedAt);
