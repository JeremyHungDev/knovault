using Knovault.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Knovault.Infrastructure.Persistence.Configurations;

public class DigitalCopyConfiguration : IEntityTypeConfiguration<DigitalCopy>
{
    public void Configure(EntityTypeBuilder<DigitalCopy> builder)
    {
        // TPH 下子型別專屬欄位皆為可空欄，不可設 IsRequired（其他子型別沒有它們）
        builder.Property(c => c.Format).HasConversion<string>();
    }
}
