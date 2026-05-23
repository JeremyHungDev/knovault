namespace Knovault.Domain.Entities;

public abstract class BookCopy
{
    public Guid Id { get; protected set; }
    public Guid BookId { get; internal set; }
    public DateTimeOffset AddedAt { get; protected set; }
    public string? Notes { get; protected set; }

    protected BookCopy()
    {
        Id = Guid.NewGuid();
        AddedAt = DateTimeOffset.UtcNow;
    }

    public void SetNotes(string? notes) => Notes = notes;
}
