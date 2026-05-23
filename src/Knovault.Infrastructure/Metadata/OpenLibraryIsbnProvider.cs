using System.Text.Json;
using Knovault.Application.Metadata;
using Knovault.Application.Parsing;

namespace Knovault.Infrastructure.Metadata;

public sealed class OpenLibraryIsbnProvider : IIsbnMetadataProvider
{
    private readonly HttpClient _http;
    public OpenLibraryIsbnProvider(HttpClient http) => _http = http;

    public async Task<ParsedBookMetadata?> LookupAsync(string isbn, CancellationToken ct = default)
    {
        var url = $"https://openlibrary.org/api/books?bibkeys=ISBN:{Uri.EscapeDataString(isbn)}&format=json&jscmd=data";
        using var resp = await _http.GetAsync(url, ct);
        if (!resp.IsSuccessStatusCode) return null;

        await using var stream = await resp.Content.ReadAsStreamAsync(ct);
        using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);
        if (!doc.RootElement.TryGetProperty($"ISBN:{isbn}", out var book)) return null;

        var authors = book.TryGetProperty("authors", out var a) && a.ValueKind == JsonValueKind.Array
            ? a.EnumerateArray()
                .Select(x => x.TryGetProperty("name", out var n) ? n.GetString() ?? "" : "")
                .Where(s => s.Length > 0).ToList()
            : new List<string>();

        string? publisher = book.TryGetProperty("publishers", out var p) &&
                            p.ValueKind == JsonValueKind.Array && p.GetArrayLength() > 0 &&
                            p[0].TryGetProperty("name", out var pn)
            ? pn.GetString() : null;

        int? pages = book.TryGetProperty("number_of_pages", out var np) && np.TryGetInt32(out var pc)
            ? pc : null;

        string? coverUrl = null;
        if (book.TryGetProperty("cover", out var cover) && cover.ValueKind == JsonValueKind.Object)
        {
            coverUrl = (cover.TryGetProperty("large", out var lg) ? lg.GetString() : null)
                ?? (cover.TryGetProperty("medium", out var md) ? md.GetString() : null)
                ?? (cover.TryGetProperty("small", out var sm) ? sm.GetString() : null);
        }

        return new ParsedBookMetadata
        {
            Title = book.TryGetProperty("title", out var t) ? t.GetString() : null,
            Authors = authors,
            Publisher = publisher,
            PublishedDate = book.TryGetProperty("publish_date", out var d) ? d.GetString() : null,
            Isbn = isbn,
            PageCount = pages,
            CoverUrl = coverUrl
        };
    }
}
