using FluentAssertions;
using Knovault.Domain.Entities;
using Knovault.Domain.Enums;
using Xunit;

namespace Knovault.Domain.Tests;

public class BookCopyTests
{
    [Fact]
    public void BookAuthor_trims_name_and_keeps_order()
    {
        var a = new BookAuthor(2, "  村上春樹 ");
        a.Order.Should().Be(2);
        a.Name.Should().Be("村上春樹");
    }

    [Fact]
    public void DigitalCopy_stores_file_info_and_sets_scanned()
    {
        var folderId = Guid.NewGuid();
        var copy = new DigitalCopy("C:/books/a.epub", BookFormat.Epub, 1024, "hash123",
            DateTimeOffset.UtcNow, folderId);

        copy.FilePath.Should().Be("C:/books/a.epub");
        copy.Format.Should().Be(BookFormat.Epub);
        copy.FileSizeBytes.Should().Be(1024);
        copy.FileHash.Should().Be("hash123");
        copy.LibraryFolderId.Should().Be(folderId);
        copy.LastScannedAt.Should().NotBeNull();
        copy.IsMissing.Should().BeFalse();
    }

    [Fact]
    public void DigitalCopy_UpdatePath_clears_missing()
    {
        var copy = new DigitalCopy("C:/old.epub", BookFormat.Epub, 1, "h", DateTimeOffset.UtcNow, null);
        copy.MarkMissing();
        copy.IsMissing.Should().BeTrue();

        copy.UpdatePath("C:/new.epub");
        copy.FilePath.Should().Be("C:/new.epub");
        copy.IsMissing.Should().BeFalse();
    }

    [Fact]
    public void DigitalCopy_rejects_blank_path_or_hash()
    {
        var act1 = () => new DigitalCopy("", BookFormat.Pdf, 1, "h", DateTimeOffset.UtcNow, null);
        var act2 = () => new DigitalCopy("C:/a.pdf", BookFormat.Pdf, 1, "", DateTimeOffset.UtcNow, null);
        act1.Should().Throw<ArgumentException>();
        act2.Should().Throw<ArgumentException>();
    }
}
