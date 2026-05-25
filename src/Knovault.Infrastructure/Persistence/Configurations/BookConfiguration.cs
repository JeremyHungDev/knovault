using Knovault.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Knovault.Infrastructure.Persistence.Configurations;

public class BookConfiguration : IEntityTypeConfiguration<Book>
{
    public void Configure(EntityTypeBuilder<Book> builder)
    {
        builder.ToTable("Books");
        builder.HasKey(b => b.Id);
        builder.Property(b => b.Title).IsRequired();
        builder.Property(b => b.ReadingStatus).HasConversion<string>();

        // 作者：owned 有序集合，對應私有欄位 _authors
        builder.OwnsMany(b => b.Authors, a =>
        {
            a.ToTable("BookAuthors");
            a.WithOwner().HasForeignKey("BookId");
            a.HasKey("BookId", nameof(BookAuthor.Order)); // 複合鍵，免去無法自動產生的 shadow Id
            a.Property(x => x.Order).ValueGeneratedNever(); // Order 由領域設定，非 DB 產生
            a.Property(x => x.Name).IsRequired();
        });
        builder.Metadata.FindNavigation(nameof(Book.Authors))!
            .SetPropertyAccessMode(PropertyAccessMode.Field);

        // 版本：TPH 一對多，對應私有欄位 _copies
        builder.HasMany(b => b.Copies)
            .WithOne()
            .HasForeignKey(c => c.BookId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.Metadata.FindNavigation(nameof(Book.Copies))!
            .SetPropertyAccessMode(PropertyAccessMode.Field);

        // 標籤：多對多（skip navigation），對應私有欄位 _tags
        builder.HasMany(b => b.Tags).WithMany();
        builder.Metadata.FindSkipNavigation(nameof(Book.Tags))!
            .SetPropertyAccessMode(PropertyAccessMode.Field);

        // 衍生旗標不入庫
        builder.Ignore(b => b.HasDigital);
        builder.Ignore(b => b.HasPhysical);
    }
}
