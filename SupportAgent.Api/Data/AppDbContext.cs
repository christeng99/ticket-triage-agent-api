using Microsoft.EntityFrameworkCore;
using SupportAgent.Api.Domain;

namespace SupportAgent.Api.Data
{
    public class AppDbContext: DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options): base(options) { }
        public DbSet<Ticket> Tickets => Set<Ticket>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            var t = modelBuilder.Entity<Ticket>();

            t.HasKey(x => x.Id);

            t.Property(x => x.Title).IsRequired().HasMaxLength(200);
            t.Property(x => x.Description).IsRequired();

            t.Property(x => x.Status).HasConversion<string>().HasMaxLength(32);

            t.Property(x => x.Category).HasMaxLength(64);
            t.Property(x => x.Priority).HasMaxLength(16);
        }
    }
}
