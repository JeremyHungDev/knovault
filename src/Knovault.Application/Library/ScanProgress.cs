namespace Knovault.Application.Library;

public sealed record ScanProgress(int Processed, int Total, string? CurrentFile);
