using Knovault.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Knovault.Infrastructure.Persistence.Configurations;

public class ExternalReviewConfiguration : IEntityTypeConfiguration<ExternalReview>
{
    public void Configure(EntityTypeBuilder<ExternalReview> builder)
    {
        builder.ToTable("ExternalReviews");
        builder.HasKey(r => r.Id);
        builder.Property(r => r.BookId).IsRequired();
        builder.Property(r => r.Source).HasConversion<string>().IsRequired();
        builder.Property(r => r.FetchedAt).IsRequired();
        builder.HasIndex(r => new { r.BookId, r.Source });
    }
}
