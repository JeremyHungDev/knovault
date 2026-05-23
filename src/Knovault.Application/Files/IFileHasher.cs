namespace Knovault.Application.Files;

public interface IFileHasher
{
    /// <summary>快速雜湊：大小 + 前 1MB 的 SHA-256，足以去重/偵測移動。</summary>
    Task<string> ComputeQuickHashAsync(string filePath, CancellationToken ct = default);
}
