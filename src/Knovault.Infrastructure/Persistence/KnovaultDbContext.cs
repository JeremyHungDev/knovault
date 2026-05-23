using Knovault.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Knovault.Infrastructure.Persistence;

public class KnovaultDbContext : DbContext
{
    public KnovaultDbContext(DbContextOptions<KnovaultDbContext> options) : base(options) { }

    public DbSet<Book> Books => Set<Book>();
    public DbSet<Tag> Tags => Set<Tag>();
    public DbSet<LibraryFolder> LibraryFolders => Set<LibraryFolder>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(KnovaultDbContext).Assembly);
    }
}
