using FluentAssertions;
using Knovault.Infrastructure.Parsing;
using Knovault.Infrastructure.Tests.Fixtures;
using Xunit;

namespace Knovault.Infrastructure.Tests;

public class PdfMetadataParserTests
{
    [Fact]
    public void CanParse_matches_pdf_extension()
    {
        var parser = new PdfMetadataParser();
        parser.CanParse("a.pdf").Should().BeTrue();
        parser.CanParse("a.PDF").Should().BeTrue();
        parser.CanParse("a.epub").Should().BeFalse();
    }

    [Fact]
    public async Task Parses_title_author_and_page_count()
    {
        var parser = new PdfMetadataParser();
        var path = PdfFixtureBuilder.CreatePdf("PDF 測試", "PDF 作者", pageCount: 3);
        try
        {
            var meta = await parser.ParseAsync(path);
            meta.Title.Should().Be("PDF 測試");
            meta.Authors.Should().ContainSingle().Which.Should().Be("PDF 作者");
            meta.PageCount.Should().Be(3);
        }
        finally { File.Delete(path); }
    }
}
