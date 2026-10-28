using Microsoft.EntityFrameworkCore;

namespace ShortLinks.Infrastructure.Persistence;

public sealed class LinkDbContext : DbContext
{
    public LinkDbContext(DbContextOptions<LinkDbContext> options)
        : base(options)
    {
    }

    public DbSet<LinkEntity> Links => Set<LinkEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var link = modelBuilder.Entity<LinkEntity>();

        link.ToTable("Links", table =>
            table.HasCheckConstraint(
                "CK_Links_AccessCount_NonNegative",
                "\"AccessCount\" >= 0"));

        link.HasKey(entity => entity.Id);

        link.Property(entity => entity.Code)
            .IsRequired()
            .HasMaxLength(20)
            .UseCollation("BINARY");

        link.HasIndex(entity => entity.Code)
            .IsUnique();

        link.Property(entity => entity.DestinationUrl)
            .IsRequired()
            .HasMaxLength(2048);

        link.Property(entity => entity.CreatedAtUtc)
            .IsRequired();

        link.Property(entity => entity.AccessCount)
            .IsRequired()
            .HasDefaultValue(0L);

        link.Property(entity => entity.ManagementKeyHash)
            .IsRequired()
            .HasMaxLength(64);

        link.Property(entity => entity.CreatorIpFingerprint)
            .IsRequired()
            .HasMaxLength(64);

        link.HasIndex(entity => new
        {
            entity.CreatorIpFingerprint,
            entity.CreatedAtUtc
        });

        base.OnModelCreating(modelBuilder);
    }
}