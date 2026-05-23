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
        builder.HasDiscriminator<string>("CopyKind")
            .HasValue<DigitalCopy>("Digital")
            .HasValue<PhysicalCopy>("Physical");
    }
}
