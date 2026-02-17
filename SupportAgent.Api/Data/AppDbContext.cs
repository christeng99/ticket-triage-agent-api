using Microsoft.EntityFrameworkCore;
using SupportAgent.Api.Domain;

namespace SupportAgent.Api.Data
{
    public class AppDbContext: DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options): base(options) { }
        public DbSet<Ticket> Tickets => Set<Ticket>();
        public DbSet<TicketAgentActionLog> TicketAgentActionLogs => Set<TicketAgentActionLog>();
        public DbSet<KbArticle> KbArticles => Set<KbArticle>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            var t = modelBuilder.Entity<Ticket>();

            t.HasKey(x => x.Id);

            t.Property(x => x.Title).IsRequired().HasMaxLength(200);
            t.Property(x => x.Description).IsRequired();

            t.Property(x => x.Status).HasConversion<string>().HasMaxLength(32);

            t.Property(x => x.Category).HasMaxLength(64);
            t.Property(x => x.Priority).HasMaxLength(16);

            t.Property(x => x.AgentNotes).HasMaxLength(1024);

            modelBuilder.Entity<TicketAgentActionLog>(e =>
            {
                e.HasKey(x => x.Id);
                e.Property(x => x.StepName).IsRequired().HasMaxLength(120);
            });

            modelBuilder.Entity<KbArticle>(e =>
            {
                e.HasKey(x => x.Id);
                e.Property(x => x.Title).IsRequired().HasMaxLength(200);
                e.Property(x => x.Body).IsRequired();
            });
        }
    }
}
