using FluentAssertions;
using Knovault.Infrastructure.Covers;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Xunit;

namespace Knovault.Infrastructure.Tests;

public class CoverStorageTests
{
    private static byte[] SamplePng()
    {
        using var img = new Image<Rgba32>(4, 4);
        using var ms = new MemoryStream();
        img.SaveAsPng(ms);
        return ms.ToArray();
    }

    [Fact]
    public async Task SaveAsync_writes_cover_and_thumbnail()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"covers_{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        try
        {
            var store = new CoverStorage(dir);
            var bookId = Guid.NewGuid();

            var rel = await store.SaveAsync(bookId, SamplePng(), "image/png");

            rel.Should().Contain(bookId.ToString("N"));
            File.Exists(Path.Combine(dir, rel)).Should().BeTrue();
            File.Exists(Path.Combine(dir, $"{bookId:N}_thumb.jpg")).Should().BeTrue();
        }
        finally { Directory.Delete(dir, recursive: true); }
    }
}
