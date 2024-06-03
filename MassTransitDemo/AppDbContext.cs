using MassTransit;
using Microsoft.EntityFrameworkCore;

namespace MassTransitDemo;

public class AppDbContext: DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options): base(options)
    {
    }
    
    public DbSet<Referral> Referrals => Set<Referral>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Referral>(m =>
        {
            m.HasKey(x => x.Key);
            m.Property(x => x.Name).HasMaxLength(128).IsRequired();
            m.Property(x => x.Status).HasConversion<short>().IsRequired();
        });

        modelBuilder.AddInboxStateEntity();
        modelBuilder.AddOutboxMessageEntity();
        modelBuilder.AddOutboxStateEntity();
    }
}