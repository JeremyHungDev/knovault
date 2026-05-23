using FluentAssertions;
using Knovault.Domain.Entities;
using Xunit;

namespace Knovault.Domain.Tests;

public class TagTests
{
    [Fact]
    public void Constructor_trims_name_and_assigns_id()
    {
        var tag = new Tag("  哲學  ");
        tag.Name.Should().Be("哲學");
        tag.Id.Should().NotBe(Guid.Empty);
        tag.CreatedAt.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_rejects_blank_name(string name)
    {
        var act = () => new Tag(name);
        act.Should().Throw<ArgumentException>();
    }
}
