namespace Knovault.Api.Contracts;

public sealed record ScanReportDto(int Added, int Updated, int Skipped, int MarkedMissing, IReadOnlyList<string> Failures);
