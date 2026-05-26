namespace Knovault.Application.Covers;

public interface ICoverStore
{
    /// <summary>儲存原圖與縮圖，回傳原圖相對檔名（如 "{bookId}.png"）。</summary>
    Task<string> SaveAsync(Guid bookId, byte[] imageBytes, string? contentType, CancellationToken ct = default);

    /// <summary>封面檔所在目錄（讀取時用）。</summary>
    string CoversDirectory { get; }
}
