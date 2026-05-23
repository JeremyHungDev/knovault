using System.IO.Compression;
using System.Text;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace Knovault.Infrastructure.Tests.Fixtures;

/// <summary>以程式組出最小但合法的 EPUB3，回傳暫存檔路徑。</summary>
public static class EpubFixtureBuilder
{
    public static string CreateMinimalEpub()
    {
        var path = Path.Combine(Path.GetTempPath(), $"epub_{Guid.NewGuid():N}.epub");
        using (var zip = ZipFile.Open(path, ZipArchiveMode.Create))
        {
            AddEntry(zip, "mimetype", "application/epub+zip");
            AddEntry(zip, "META-INF/container.xml", """
                <?xml version="1.0"?>
                <container version="1.0" xmlns="urn:oasis:names:tc:opendocument:xmlns:container">
                  <rootfiles>
                    <rootfile full-path="OEBPS/content.opf" media-type="application/oebps-package+xml"/>
                  </rootfiles>
                </container>
                """);
            AddEntry(zip, "OEBPS/content.opf", """
                <?xml version="1.0" encoding="utf-8"?>
                <package xmlns="http://www.idpf.org/2007/opf" version="3.0" unique-identifier="bookid">
                  <metadata xmlns:dc="http://purl.org/dc/elements/1.1/">
                    <dc:title>測試書名</dc:title>
                    <dc:creator>作者一</dc:creator>
                    <dc:creator>作者二</dc:creator>
                    <dc:language>zh-TW</dc:language>
                    <dc:publisher>測試出版社</dc:publisher>
                    <dc:date>2021-05-01</dc:date>
                    <dc:identifier id="bookid">9781234567890</dc:identifier>
                    <dc:description>一本測試書。</dc:description>
                  </metadata>
                  <manifest>
                    <item id="cover" href="cover.png" media-type="image/png" properties="cover-image"/>
                    <item id="nav" href="nav.xhtml" media-type="application/xhtml+xml" properties="nav"/>
                    <item id="c1" href="c1.xhtml" media-type="application/xhtml+xml"/>
                  </manifest>
                  <spine>
                    <itemref idref="c1"/>
                  </spine>
                </package>
                """);
            AddEntry(zip, "OEBPS/nav.xhtml", """
                <?xml version="1.0" encoding="utf-8"?>
                <html xmlns="http://www.w3.org/1999/xhtml" xmlns:epub="http://www.idpf.org/2007/ops">
                  <body>
                    <nav epub:type="toc">
                      <ol>
                        <li><a href="c1.xhtml#ch1">第一章</a></li>
                        <li><a href="c1.xhtml#ch2">第二章</a></li>
                      </ol>
                    </nav>
                  </body>
                </html>
                """);
            AddEntry(zip, "OEBPS/c1.xhtml", "<html><body><p>內容</p></body></html>");
            AddBinaryEntry(zip, "OEBPS/cover.png", MinimalPng());
        }
        return path;
    }

    private static void AddEntry(ZipArchive zip, string name, string content)
    {
        var entry = zip.CreateEntry(name);
        using var s = entry.Open();
        var bytes = Encoding.UTF8.GetBytes(content);
        s.Write(bytes, 0, bytes.Length);
    }

    private static void AddBinaryEntry(ZipArchive zip, string name, byte[] content)
    {
        var entry = zip.CreateEntry(name);
        using var s = entry.Open();
        s.Write(content, 0, content.Length);
    }

    // 以 ImageSharp 產生合法的 2x2 PNG（避免手寫 base64 的 CRC 問題）
    private static byte[] MinimalPng()
    {
        using var img = new Image<Rgba32>(2, 2);
        using var ms = new MemoryStream();
        img.SaveAsPng(ms);
        return ms.ToArray();
    }
}
