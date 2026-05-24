using Knovault.Domain.Enums;
using Knovault.Domain.ValueObjects;

namespace Knovault.Domain.Entities;

public class Book
{
    public Guid Id { get; private set; }
    public string Title { get; private set; }
    public string? Subtitle { get; private set; }
    public string? Language { get; private set; }
    public string? Publisher { get; private set; }
    public string? PublishedDate { get; private set; }
    public string? Description { get; private set; }
    public string? Isbn { get; private set; }
    public string? CoverPath { get; private set; }
    public ReadingStatus ReadingStatus { get; private set; }
    public ReadingProgress Progress { get; private set; }
    // 形式只是紀錄：實體 = 一個旗標（無位置/版本管理）；電子 = 是否有數位檔
    public bool IsPhysical { get; private set; }
    public string? PhysicalLocation { get; private set; }
    public string? PhysicalNotes { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    private readonly List<BookAuthor> _authors = new();
    public IReadOnlyList<BookAuthor> Authors => _authors.OrderBy(a => a.Order).ToList();

    private readonly List<BookCopy> _copies = new();
    public IReadOnlyList<BookCopy> Copies => _copies;

    private readonly List<Tag> _tags = new();
    public IReadOnlyList<Tag> Tags => _tags;

    public bool HasDigital => _copies.Any(c => c is DigitalCopy);
    public bool HasPhysical => IsPhysical;

    private Book() { Title = null!; Progress = ReadingProgress.Empty; } // EF

    public Book(string title)
    {
        if (string.IsNullOrWhiteSpace(title))
            throw new ArgumentException("Title is required.", nameof(title));

        Id = Guid.NewGuid();
        Title = title.Trim();
        ReadingStatus = ReadingStatus.None;
        Progress = ReadingProgress.Empty;
        CreatedAt = UpdatedAt = DateTimeOffset.UtcNow;
    }

    private void Touch() => UpdatedAt = DateTimeOffset.UtcNow;

    public void SetAuthors(IEnumerable<string> names)
    {
        _authors.Clear();
        var order = 0;
        foreach (var name in names.Where(n => !string.IsNullOrWhiteSpace(n)))
            _authors.Add(new BookAuthor(order++, name));
        Touch();
    }

    public void AddCopy(BookCopy copy)
    {
        copy.BookId = Id;
        _copies.Add(copy);
        Touch();
    }

    public void RemoveCopy(BookCopy copy)
    {
        _copies.Remove(copy);
        Touch();
    }

    public void AddTag(Tag tag)
    {
        if (_tags.Any(t => t.Id == tag.Id)) return;
        _tags.Add(tag);
        Touch();
    }

    public void RemoveTag(Tag tag)
    {
        _tags.RemoveAll(t => t.Id == tag.Id);
        Touch();
    }

    public void SetReadingStatus(ReadingStatus status)
    {
        ReadingStatus = status;
        Touch();
    }

    public void SetProgress(ReadingProgress progress)
    {
        Progress = progress ?? ReadingProgress.Empty;
        Touch();
    }

    public void SetCoverPath(string? coverPath)
    {
        CoverPath = coverPath;
        Touch();
    }

    public void SetPhysical(bool isPhysical)
    {
        IsPhysical = isPhysical;
        Touch();
    }

    public void SetPhysicalInfo(bool isPhysical, string? location, string? notes)
    {
        IsPhysical = isPhysical;
        PhysicalLocation = isPhysical ? location?.Trim() : null;
        PhysicalNotes = isPhysical ? notes?.Trim() : null;
        Touch();
    }

    public void UpdateMetadata(string title, string? subtitle, string? language, string? publisher,
        string? publishedDate, string? description, string? isbn)
    {
        if (string.IsNullOrWhiteSpace(title))
            throw new ArgumentException("Title is required.", nameof(title));

        Title = title.Trim();
        Subtitle = subtitle;
        Language = language;
        Publisher = publisher;
        PublishedDate = publishedDate;
        Description = description;
        Isbn = isbn;
        Touch();
    }
}
