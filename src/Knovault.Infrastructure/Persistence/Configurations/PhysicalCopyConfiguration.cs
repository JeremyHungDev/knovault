using Knovault.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Knovault.Infrastructure.Persistence.Configurations;

public class PhysicalCopyConfiguration : IEntityTypeConfiguration<PhysicalCopy>
{
    public void Configure(EntityTypeBuilder<PhysicalCopy> builder)
    {
        // Location/AcquiredDate 由慣例對應為可空欄，無需額外設定
    }
}
