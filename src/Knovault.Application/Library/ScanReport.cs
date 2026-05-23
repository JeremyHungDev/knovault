namespace Knovault.Application.Library;

public sealed record ScanFailure(string FilePath, string Reason);

public sealed class ScanReport
{
    public int Added { get; set; }
    public int Updated { get; set; }
    public int Skipped { get; set; }
    public int MarkedMissing { get; set; }
    public List<ScanFailure> Failures { get; } = new();
}
