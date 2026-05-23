using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.Writer;

namespace Knovault.Infrastructure.Tests.Fixtures;

/// <summary>用 PdfPig builder 產生含標題/作者與 N 頁的 PDF，回傳暫存檔路徑。</summary>
public static class PdfFixtureBuilder
{
    public static string CreatePdf(string title, string author, int pageCount)
    {
        var builder = new PdfDocumentBuilder();
        builder.DocumentInformation.Title = title;
        builder.DocumentInformation.Author = author;
        for (var i = 0; i < pageCount; i++)
            builder.AddPage(PageSize.A4);

        var bytes = builder.Build();
        var path = Path.Combine(Path.GetTempPath(), $"pdf_{Guid.NewGuid():N}.pdf");
        File.WriteAllBytes(path, bytes);
        return path;
    }
}
