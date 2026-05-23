using Knovault.Application.Parsing;
using UglyToad.PdfPig;

namespace Knovault.Infrastructure.Parsing;

public sealed class PdfMetadataParser : IBookFileParser
{
    public bool CanParse(string filePath) =>
        string.Equals(Path.GetExtension(filePath), ".pdf", StringComparison.OrdinalIgnoreCase);

    public Task<ParsedBookMetadata> ParseAsync(string filePath, CancellationToken ct = default)
    {
        using var doc = PdfDocument.Open(filePath);
        var info = doc.Information;

        var authors = string.IsNullOrWhiteSpace(info.Author)
            ? Array.Empty<string>()
            : new[] { info.Author.Trim() };

        var result = new ParsedBookMetadata
        {
            Title = string.IsNullOrWhiteSpace(info.Title) ? null : info.Title.Trim(),
            Authors = authors,
            PageCount = doc.NumberOfPages
        };
        return Task.FromResult(result);
    }
}
