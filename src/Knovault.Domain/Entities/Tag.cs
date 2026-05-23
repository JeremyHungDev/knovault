namespace Knovault.Domain.Entities;

public class Tag
{
    public Guid Id { get; private set; }
    public string Name { get; private set; }
    public string? Color { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    private Tag() { Name = null!; } // EF

    public Tag(string name, string? color = null)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Tag name is required.", nameof(name));

        Id = Guid.NewGuid();
        Name = name.Trim();
        Color = color;
        CreatedAt = DateTimeOffset.UtcNow;
    }

    public void Rename(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Tag name is required.", nameof(name));
        Name = name.Trim();
    }

    public void SetColor(string? color) => Color = color;
}
