using FluentAssertions;
using Knovault.Infrastructure.Files;
using Xunit;

namespace Knovault.Infrastructure.Tests;

public class FilenameTitleCleanerTests
{
    [Theory]
    [InlineData("C:/books/the_great_book.epub", "the great book")]
    [InlineData("D:/x/Clean.Architecture.pdf", "Clean Architecture")]
    [InlineData("/tmp/  spaced   name .epub", "spaced name")]
    public void Clean_strips_extension_and_normalizes(string path, string expected)
    {
        FilenameTitleCleaner.Clean(path).Should().Be(expected);
    }

    [Fact]
    public void Clean_returns_untitled_for_empty_name()
    {
        FilenameTitleCleaner.Clean("C:/books/.epub").Should().Be("Untitled");
    }
}
