using FluentAssertions;
using Knovault.Application.Parsing;
using Knovault.Infrastructure.Parsing;
using Knovault.Infrastructure.Tests.Fixtures;
using Xunit;

namespace Knovault.Infrastructure.Tests;

public class BookParsingServiceTests
{
    private static BookParsingService NewService() =>
        new(new IBookFileParser[] { new EpubMetadataParser(), new PdfMetadataParser() });

    [Fact]
    public async Task Parses_supported_epub()
    {
        var svc = NewService();
        var path = EpubFixtureBuilder.CreateMinimalEpub();
        try
        {
            var (meta, failed) = await svc.ParseAsync(path);
            failed.Should().BeFalse();
            meta.Title.Should().Be("測試書名");
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public async Task Unsupported_extension_falls_back_to_filename()
    {
        var svc = NewService();
        var (meta, failed) = await svc.ParseAsync("C:/books/some_unknown_book.mobi");
        failed.Should().BeTrue();
        meta.Title.Should().Be("some unknown book");
    }

    [Fact]
    public async Task Corrupt_file_falls_back_to_filename()
    {
        var svc = NewService();
        var path = Path.Combine(Path.GetTempPath(), $"broken_{Guid.NewGuid():N}.epub");
        await File.WriteAllTextAsync(path, "not a real zip");
        try
        {
            var (meta, failed) = await svc.ParseAsync(path);
            failed.Should().BeTrue();
            meta.Title.Should().StartWith("broken");
        }
        finally { File.Delete(path); }
    }
}
