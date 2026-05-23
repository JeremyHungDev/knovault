using Knovault.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Knovault.Infrastructure.Persistence.Configurations;

public class LibraryFolderConfiguration : IEntityTypeConfiguration<LibraryFolder>
{
    public void Configure(EntityTypeBuilder<LibraryFolder> builder)
    {
        builder.ToTable("LibraryFolders");
        builder.HasKey(f => f.Id);
        builder.Property(f => f.Path).IsRequired();
        builder.HasIndex(f => f.Path).IsUnique();
    }
}
