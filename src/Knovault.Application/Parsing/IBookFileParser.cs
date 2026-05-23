namespace Knovault.Application.Parsing;

public interface IBookFileParser
{
    /// <summary>是否能解析此副檔名（如 .epub / .pdf）。</summary>
    bool CanParse(string filePath);

    /// <summary>解析；失敗時擲例外，由呼叫端決定 fallback。</summary>
    Task<ParsedBookMetadata> ParseAsync(string filePath, CancellationToken ct = default);
}
