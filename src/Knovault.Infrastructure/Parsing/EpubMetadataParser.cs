using System.IO.Compression;
using System.Xml.Linq;
using Knovault.Application.Parsing;

namespace Knovault.Infrastructure.Parsing;

public sealed class EpubMetadataParser : IBookFileParser
{
    private static readonly XNamespace Opf = "http://www.idpf.org/2007/opf";
    private static readonly XNamespace Dc = "http://purl.org/dc/elements/1.1/";
    private static readonly XNamespace Cnt = "urn:oasis:names:tc:opendocument:xmlns:container";
    private static readonly XNamespace Xhtml = "http://www.w3.org/1999/xhtml";

    public bool CanParse(string filePath) =>
        string.Equals(Path.GetExtension(filePath), ".epub", StringComparison.OrdinalIgnoreCase);

    public Task<ParsedBookMetadata> ParseAsync(string filePath, CancellationToken ct = default)
    {
        using var zip = ZipFile.OpenRead(filePath);

        var opfPath = GetOpfPath(zip);
        var opfDir = Path.GetDirectoryName(opfPath)?.Replace('\\', '/') ?? "";
        var opf = LoadXml(zip, opfPath);

        var metadata = opf.Root!.Element(Opf + "metadata")
            ?? opf.Root.Elements().First(e => e.Name.LocalName == "metadata");

        var authors = metadata.Elements(Dc + "creator").Select(e => e.Value.Trim())
            .Where(s => s.Length > 0).ToList();

        var manifest = opf.Root.Element(Opf + "manifest")!;
        var (coverBytes, coverType) = ReadCover(zip, manifest, metadata, opfDir);
        var toc = ReadToc(zip, manifest, opfDir);

        var result = new ParsedBookMetadata
        {
            Title = metadata.Element(Dc + "title")?.Value.Trim(),
            Authors = authors,
            Language = metadata.Element(Dc + "language")?.Value.Trim(),
            Publisher = metadata.Element(Dc + "publisher")?.Value.Trim(),
            PublishedDate = metadata.Element(Dc + "date")?.Value.Trim(),
            Isbn = metadata.Elements(Dc + "identifier").Select(e => e.Value.Trim())
                .FirstOrDefault(LooksLikeIsbn),
            Description = metadata.Element(Dc + "description")?.Value.Trim(),
            CoverImage = coverBytes,
            CoverContentType = coverType,
            Toc = toc
        };
        return Task.FromResult(result);
    }

    private static string GetOpfPath(ZipArchive zip)
    {
        var container = LoadXml(zip, "META-INF/container.xml");
        var rootfile = container.Descendants(Cnt + "rootfile").First();
        return rootfile.Attribute("full-path")!.Value;
    }

    private static (byte[]? bytes, string? type) ReadCover(
        ZipArchive zip, XElement manifest, XElement metadata, string opfDir)
    {
        // EPUB3: properties 含 cover-image
        var coverItem = manifest.Elements(Opf + "item")
            .FirstOrDefault(i => (i.Attribute("properties")?.Value ?? "").Split(' ').Contains("cover-image"));

        // EPUB2: <meta name="cover" content="id"/>
        if (coverItem is null)
        {
            var coverId = metadata.Elements(Opf + "meta")
                .FirstOrDefault(m => (string?)m.Attribute("name") == "cover")?.Attribute("content")?.Value;
            if (coverId is not null)
                coverItem = manifest.Elements(Opf + "item")
                    .FirstOrDefault(i => (string?)i.Attribute("id") == coverId);
        }

        if (coverItem is null) return (null, null);

        var href = coverItem.Attribute("href")!.Value;
        var entry = zip.GetEntry(CombineZipPath(opfDir, href));
        if (entry is null) return (null, null);

        using var s = entry.Open();
        using var ms = new MemoryStream();
        s.CopyTo(ms);
        return (ms.ToArray(), (string?)coverItem.Attribute("media-type"));
    }

    private static List<TocEntry> ReadToc(ZipArchive zip, XElement manifest, string opfDir)
    {
        var navItem = manifest.Elements(Opf + "item")
            .FirstOrDefault(i => (i.Attribute("properties")?.Value ?? "").Split(' ').Contains("nav"));
        if (navItem is null) return new();

        var navDoc = LoadXml(zip, CombineZipPath(opfDir, navItem.Attribute("href")!.Value));
        var nav = navDoc.Descendants().FirstOrDefault(e => e.Name.LocalName == "nav");
        var ol = nav?.Descendants().FirstOrDefault(e => e.Name.LocalName == "ol");
        return ol is null ? new() : ParseNavList(ol);
    }

    private static List<TocEntry> ParseNavList(XElement ol)
    {
        var result = new List<TocEntry>();
        foreach (var li in ol.Elements().Where(e => e.Name.LocalName == "li"))
        {
            var a = li.Elements().FirstOrDefault(e => e.Name.LocalName == "a");
            var childOl = li.Elements().FirstOrDefault(e => e.Name.LocalName == "ol");
            result.Add(new TocEntry
            {
                Title = a?.Value.Trim() ?? "",
                Href = (string?)a?.Attribute("href"),
                Children = childOl is null ? new() : ParseNavList(childOl)
            });
        }
        return result;
    }

    private static bool LooksLikeIsbn(string value)
    {
        var digitCount = value.Count(char.IsDigit);
        return digitCount is 10 or 13;
    }

    private static string CombineZipPath(string dir, string href) =>
        (string.IsNullOrEmpty(dir) ? href : $"{dir}/{href}").Replace('\\', '/');

    private static XDocument LoadXml(ZipArchive zip, string entryPath)
    {
        var entry = zip.GetEntry(entryPath) ?? throw new InvalidDataException($"EPUB 缺少 {entryPath}");
        using var s = entry.Open();
        return XDocument.Load(s);
    }
}
