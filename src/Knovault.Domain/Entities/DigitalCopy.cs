using Knovault.Domain.Enums;

namespace Knovault.Domain.Entities;

public class DigitalCopy : BookCopy
{
    public string FilePath { get; private set; }
    public BookFormat Format { get; private set; }
    public long FileSizeBytes { get; private set; }
    public string FileHash { get; private set; }
    public DateTimeOffset FileLastModified { get; private set; }
    public string? TocJson { get; private set; }
    public Guid? LibraryFolderId { get; private set; }
    public DateTimeOffset? LastScannedAt { get; private set; }
    public bool IsMissing { get; private set; }
    public bool ParseFailed { get; private set; }

    private DigitalCopy() { FilePath = null!; FileHash = null!; } // EF

    public DigitalCopy(string filePath, BookFormat format, long fileSizeBytes, string fileHash,
        DateTimeOffset fileLastModified, Guid? libraryFolderId)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            throw new ArgumentException("FilePath is required.", nameof(filePath));
        if (string.IsNullOrWhiteSpace(fileHash))
            throw new ArgumentException("FileHash is required.", nameof(fileHash));

        FilePath = filePath;
        Format = format;
        FileSizeBytes = fileSizeBytes;
        FileHash = fileHash;
        FileLastModified = fileLastModified;
        LibraryFolderId = libraryFolderId;
        LastScannedAt = DateTimeOffset.UtcNow;
    }

    public void UpdatePath(string newPath)
    {
        if (string.IsNullOrWhiteSpace(newPath))
            throw new ArgumentException("FilePath is required.", nameof(newPath));
        FilePath = newPath;
        IsMissing = false;
        LastScannedAt = DateTimeOffset.UtcNow;
    }

    public void MarkMissing() => IsMissing = true;
    public void MarkParseFailed() => ParseFailed = true;
    public void SetToc(string? tocJson) => TocJson = tocJson;
}
