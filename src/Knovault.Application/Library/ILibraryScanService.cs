namespace Knovault.Application.Library;

public interface ILibraryScanService
{
    /// <summary>掃描所有啟用的書庫資料夾。</summary>
    Task<ScanReport> ScanAsync(CancellationToken ct = default);

    /// <summary>掃描並逐檔回報進度。</summary>
    Task<ScanReport> ScanAsync(Func<ScanProgress, Task>? onProgress, CancellationToken ct = default);
}
