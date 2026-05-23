using FluentAssertions;
using Knovault.Domain.ValueObjects;
using Xunit;

namespace Knovault.Domain.Tests;

public class ReadingProgressTests
{
    [Fact]
    public void Empty_has_all_nulls()
    {
        var p = ReadingProgress.Empty;
        p.Percent.Should().BeNull();
        p.CurrentPage.Should().BeNull();
        p.TotalPages.Should().BeNull();
    }

    [Fact]
    public void Create_with_valid_values_succeeds()
    {
        var p = ReadingProgress.Create(percent: 45, currentPage: 90, totalPages: 200);
        p.Percent.Should().Be(45);
        p.CurrentPage.Should().Be(90);
        p.TotalPages.Should().Be(200);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(101)]
    public void Create_rejects_out_of_range_percent(int percent)
    {
        var act = () => ReadingProgress.Create(percent: percent);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Create_rejects_current_page_greater_than_total()
    {
        var act = () => ReadingProgress.Create(currentPage: 300, totalPages: 200);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_rejects_negative_pages()
    {
        var act = () => ReadingProgress.Create(currentPage: -5);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }
}
