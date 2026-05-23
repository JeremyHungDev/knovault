using FluentAssertions;
using Knovault.Domain.Entities;
using Xunit;

namespace Knovault.Domain.Tests;

public class LibraryFolderTests
{
    [Fact]
    public void Constructor_defaults_enabled_and_sets_path()
    {
        var folder = new LibraryFolder(@"D:\Books", "我的書庫");
        folder.Id.Should().NotBe(Guid.Empty);
        folder.Path.Should().Be(@"D:\Books");
        folder.DisplayName.Should().Be("我的書庫");
        folder.Enabled.Should().BeTrue();
        folder.LastScannedAt.Should().BeNull();
    }

    [Fact]
    public void Constructor_rejects_blank_path()
    {
        var act = () => new LibraryFolder("  ");
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void MarkScanned_sets_timestamp()
    {
        var folder = new LibraryFolder(@"D:\Books");
        folder.MarkScanned();
        folder.LastScannedAt.Should().NotBeNull();
    }
}
