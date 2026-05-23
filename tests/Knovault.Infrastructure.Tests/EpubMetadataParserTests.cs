using FluentAssertions;
using Knovault.Infrastructure.Parsing;
using Knovault.Infrastructure.Tests.Fixtures;
using Xunit;

namespace Knovault.Infrastructure.Tests;

public class EpubMetadataParserTests
{
    [Fact]
    public void CanParse_matches_epub_extension()
    {
        var parser = new EpubMetadataParser();
        parser.CanParse("a.epub").Should().BeTrue();
        parser.CanParse("a.EPUB").Should().BeTrue();
        parser.CanParse("a.pdf").Should().BeFalse();
    }

    [Fact]
    public async Task Parses_metadata_cover_and_toc()
    {
        var parser = new EpubMetadataParser();
        var path = EpubFixtureBuilder.CreateMinimalEpub();
        try
        {
            var meta = await parser.ParseAsync(path);

            meta.Title.Should().Be("測試書名");
            meta.Authors.Should().BeEquivalentTo(new[] { "作者一", "作者二" }, o => o.WithStrictOrdering());
            meta.Language.Should().Be("zh-TW");
            meta.Publisher.Should().Be("測試出版社");
            meta.PublishedDate.Should().Be("2021-05-01");
            meta.Isbn.Should().Be("9781234567890");
            meta.Description.Should().Be("一本測試書。");
            meta.CoverImage.Should().NotBeNullOrEmpty();
            meta.CoverContentType.Should().Be("image/png");
            meta.Toc.Should().HaveCount(2);
            meta.Toc[0].Title.Should().Be("第一章");
        }
        finally { File.Delete(path); }
    }
}
