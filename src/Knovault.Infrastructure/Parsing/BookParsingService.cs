using Knovault.Application.Parsing;
using Knovault.Infrastructure.Files;

namespace Knovault.Infrastructure.Parsing;

public sealed class BookParsingService
{
    private readonly IReadOnlyList<IBookFileParser> _parsers;

    public BookParsingService(IEnumerable<IBookFileParser> parsers) => _parsers = parsers.ToList();

    public bool IsSupported(string path) => _parsers.Any(p => p.CanParse(path));

    /// <summary>解析並套用 fallback；回傳 (元數據, 是否解析失敗)。</summary>
    public async Task<(ParsedBookMetadata Metadata, bool Failed)> ParseAsync(string path, CancellationToken ct = default)
    {
        var parser = _parsers.FirstOrDefault(p => p.CanParse(path));
        if (parser is null)
            return (new ParsedBookMetadata { Title = FilenameTitleCleaner.Clean(path) }, true);

        try
        {
            var meta = await parser.ParseAsync(path, ct);
            if (string.IsNullOrWhiteSpace(meta.Title))
                meta = meta with { Title = FilenameTitleCleaner.Clean(path) };
            return (meta, false);
        }
        catch
        {
            return (new ParsedBookMetadata { Title = FilenameTitleCleaner.Clean(path) }, true);
        }
    }
}
