using FluentAssertions;
using Knovault.Domain.Entities;
using Knovault.Domain.Enums;
using Knovault.Domain.ValueObjects;
using Xunit;

namespace Knovault.Domain.Tests;

public class BookTests
{
    private static Book NewBook() => new("Clean Architecture");

    [Fact]
    public void Constructor_sets_defaults()
    {
        var book = NewBook();
        book.Id.Should().NotBe(Guid.Empty);
        book.Title.Should().Be("Clean Architecture");
        book.ReadingStatus.Should().Be(ReadingStatus.None);
        book.Progress.Should().BeSameAs(ReadingProgress.Empty);
        book.HasDigital.Should().BeFalse();
        book.HasPhysical.Should().BeFalse();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_rejects_blank_title(string title)
    {
        var act = () => new Book(title);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void AddCopy_and_SetPhysical_set_flags()
    {
        var book = NewBook();
        var digital = new DigitalCopy("C:/a.epub", BookFormat.Epub, 1, "h", DateTimeOffset.UtcNow, null);

        book.AddCopy(digital);
        book.SetPhysical(true);

        digital.BookId.Should().Be(book.Id);
        book.Copies.Should().ContainSingle();
        book.HasDigital.Should().BeTrue();
        book.HasPhysical.Should().BeTrue();
    }

    [Fact]
    public void SetAuthors_orders_and_skips_blanks()
    {
        var book = NewBook();
        book.SetAuthors(new[] { "Robert C. Martin", "  ", "Second Author" });

        book.Authors.Should().HaveCount(2);
        book.Authors[0].Order.Should().Be(0);
        book.Authors[0].Name.Should().Be("Robert C. Martin");
        book.Authors[1].Name.Should().Be("Second Author");
    }

    [Fact]
    public void AddTag_is_idempotent_by_id()
    {
        var book = NewBook();
        var tag = new Tag("哲學");
        book.AddTag(tag);
        book.AddTag(tag);
        book.Tags.Should().HaveCount(1);
    }

    [Fact]
    public void SetProgress_and_status_update_timestamp()
    {
        var book = NewBook();
        var before = book.UpdatedAt;
        book.SetReadingStatus(ReadingStatus.Reading);
        book.SetProgress(ReadingProgress.Create(percent: 30));

        book.ReadingStatus.Should().Be(ReadingStatus.Reading);
        book.Progress.Percent.Should().Be(30);
        book.UpdatedAt.Should().BeOnOrAfter(before);
    }

    [Fact]
    public void UpdateMetadata_rejects_blank_title()
    {
        var book = NewBook();
        var act = () => book.UpdateMetadata("", null, null, null, null, null, null);
        act.Should().Throw<ArgumentException>();
    }
}
