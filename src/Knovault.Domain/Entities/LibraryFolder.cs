namespace Knovault.Domain.Entities;

public class LibraryFolder
{
    public Guid Id { get; private set; }
    public string Path { get; private set; }
    public string? DisplayName { get; private set; }
    public bool Enabled { get; private set; }
    public DateTimeOffset AddedAt { get; private set; }
    public DateTimeOffset? LastScannedAt { get; private set; }

    private LibraryFolder() { Path = null!; } // EF

    public LibraryFolder(string path, string? displayName = null)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new ArgumentException("Path is required.", nameof(path));

        Id = Guid.NewGuid();
        Path = path;
        DisplayName = displayName;
        Enabled = true;
        AddedAt = DateTimeOffset.UtcNow;
    }

    public void MarkScanned() => LastScannedAt = DateTimeOffset.UtcNow;
    public void SetEnabled(bool enabled) => Enabled = enabled;
}
