namespace Knovault.Domain.Entities;

public class BookAuthor
{
    public int Order { get; private set; }
    public string Name { get; private set; }

    private BookAuthor() { Name = null!; } // EF

    public BookAuthor(int order, string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Author name is required.", nameof(name));
        Order = order;
        Name = name.Trim();
    }
}
