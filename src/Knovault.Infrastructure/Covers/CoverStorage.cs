using Knovault.Application.Covers;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;

namespace Knovault.Infrastructure.Covers;

public sealed class CoverStorage : ICoverStore
{
    private const int ThumbnailMaxWidth = 400;
    private readonly string _root;

    public CoverStorage(string coversRootPath)
    {
        _root = coversRootPath;
        Directory.CreateDirectory(_root);
    }

    public string CoversDirectory => _root;

    public async Task<string> SaveAsync(Guid bookId, byte[] imageBytes, string? contentType, CancellationToken ct = default)
    {
        var coverName = $"{bookId:N}{ExtensionFor(contentType)}";
        await File.WriteAllBytesAsync(Path.Combine(_root, coverName), imageBytes, ct);

        using var image = Image.Load(imageBytes);
        image.Mutate(x => x.Resize(new ResizeOptions
        {
            Mode = ResizeMode.Max,
            Size = new Size(ThumbnailMaxWidth, 0)
        }));
        await image.SaveAsJpegAsync(Path.Combine(_root, $"{bookId:N}_thumb.jpg"), ct);

        return coverName;
    }

    private static string ExtensionFor(string? contentType) => contentType switch
    {
        "image/jpeg" or "image/jpg" => ".jpg",
        "image/gif" => ".gif",
        "image/webp" => ".webp",
        _ => ".png"
    };
}
