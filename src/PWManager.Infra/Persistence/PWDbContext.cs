using Microsoft.EntityFrameworkCore;
using PWManager.Domain.Vault;

namespace PWManager.Infra.Persistence;

public sealed class PWDbContext(DbContextOptions<PWDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();

    public DbSet<VaultMetadata> VaultMetadata => Set<VaultMetadata>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>(entity =>
        {
            entity.ToTable("User");
            entity.HasKey(user => user.Id);
            entity.Property(user => user.Site).IsRequired();
            entity.Property(user => user.Login).IsRequired();
            entity.Property(user => user.Password).IsRequired();
            entity.Property(user => user.CreationDate).IsRequired();
            entity.Property(user => user.LastUpdated).HasDefaultValueSql("CURRENT_TIMESTAMP");
        });

        modelBuilder.Entity<VaultMetadata>(entity =>
        {
            entity.ToTable("VaultMetadata");
            entity.HasKey(metadata => metadata.Id);
            entity.Property(metadata => metadata.Id).ValueGeneratedNever();
            entity.Property(metadata => metadata.Kdf).IsRequired();
            entity.Property(metadata => metadata.KdfSalt).IsRequired();
            entity.Property(metadata => metadata.Verifier).IsRequired();
            entity.Property(metadata => metadata.CreatedAt).IsRequired();
        });
    }
}
