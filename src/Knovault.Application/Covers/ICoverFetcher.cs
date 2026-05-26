namespace Knovault.Application.Covers;

public interface ICoverFetcher
{
    /// <summary>下載圖片 URL；失敗或非圖片回 null。</summary>
    Task<(byte[] Bytes, string? ContentType)?> FetchAsync(string url, CancellationToken ct = default);
}
