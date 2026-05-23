using FluentAssertions;
using Knovault.Infrastructure.Files;
using Xunit;

namespace Knovault.Infrastructure.Tests;

public class FileHasherTests
{
    private static async Task<string> WriteTempAsync(byte[] content)
    {
        var path = Path.Combine(Path.GetTempPath(), $"hash_{Guid.NewGuid():N}.bin");
        await File.WriteAllBytesAsync(path, content);
        return path;
    }

    [Fact]
    public async Task Same_content_yields_same_hash()
    {
        var hasher = new FileHasher();
        var a = await WriteTempAsync(new byte[] { 1, 2, 3, 4, 5 });
        var b = await WriteTempAsync(new byte[] { 1, 2, 3, 4, 5 });
        try
        {
            (await hasher.ComputeQuickHashAsync(a)).Should().Be(await hasher.ComputeQuickHashAsync(b));
        }
        finally { File.Delete(a); File.Delete(b); }
    }

    [Fact]
    public async Task Different_content_yields_different_hash()
    {
        var hasher = new FileHasher();
        var a = await WriteTempAsync(new byte[] { 1, 2, 3 });
        var b = await WriteTempAsync(new byte[] { 9, 9, 9 });
        try
        {
            (await hasher.ComputeQuickHashAsync(a)).Should().NotBe(await hasher.ComputeQuickHashAsync(b));
        }
        finally { File.Delete(a); File.Delete(b); }
    }

    [Fact]
    public async Task Hash_includes_size_prefix()
    {
        var hasher = new FileHasher();
        var a = await WriteTempAsync(new byte[] { 1, 2, 3 });
        try
        {
            (await hasher.ComputeQuickHashAsync(a)).Should().StartWith("3-");
        }
        finally { File.Delete(a); }
    }
}
