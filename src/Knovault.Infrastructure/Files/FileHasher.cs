using System.Buffers;
using System.Security.Cryptography;
using Knovault.Application.Files;

namespace Knovault.Infrastructure.Files;

public sealed class FileHasher : IFileHasher
{
    private const int SampleSize = 1024 * 1024; // 1 MB

    public async Task<string> ComputeQuickHashAsync(string filePath, CancellationToken ct = default)
    {
        var size = new FileInfo(filePath).Length;

        await using var stream = new FileStream(
            filePath, FileMode.Open, FileAccess.Read, FileShare.Read,
            bufferSize: 4096, useAsync: true);

        var buffer = ArrayPool<byte>.Shared.Rent(SampleSize);
        try
        {
            var total = 0;
            int read;
            while (total < SampleSize &&
                   (read = await stream.ReadAsync(buffer.AsMemory(total, SampleSize - total), ct)) > 0)
                total += read;

            var hash = SHA256.HashData(buffer.AsSpan(0, total));
            return $"{size}-{Convert.ToHexString(hash)}";
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }
}
