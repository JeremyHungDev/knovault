using Knovault.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Knovault.Infrastructure.Persistence.Configurations;

public class BookCopyConfiguration : IEntityTypeConfiguration<BookCopy>
{
    public void Configure(EntityTypeBuilder<BookCopy> builder)
    {
        builder.ToTable("BookCopies");
        builder.HasKey(c => c.Id);
        // 形式重構後只剩數位版本（檔案）；實體改為 Book.IsPhysical 旗標。
        builder.HasDiscriminator<string>("CopyKind")
            .HasValue<DigitalCopy>("Digital");
    }
}
