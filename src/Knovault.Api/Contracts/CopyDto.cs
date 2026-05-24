namespace Knovault.Api.Contracts;

// 形式重構後 copy 僅代表數位檔（實體已改為 Book.IsPhysical 旗標）。
public sealed record CopyDto
{
    public Guid Id { get; init; }
    public string Format { get; init; } = "";       // "Epub" | "Pdf"
    public long FileSizeBytes { get; init; }
    public bool IsMissing { get; init; }
    public bool ParseFailed { get; init; }
}
