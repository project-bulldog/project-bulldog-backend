using backend.Models;
using Microsoft.EntityFrameworkCore;

namespace backend.Data
{
    public class BulldogDbContext : DbContext
    {
        public BulldogDbContext(DbContextOptions<BulldogDbContext> options)
            : base(options)
        {
        }

        public DbSet<User> Users { get; set; }
        public DbSet<Summary> Summaries { get; set; }
        public DbSet<ActionItem> ActionItems { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Unique constraint on User.Email
            modelBuilder.Entity<User>()
                .HasIndex(u => u.Email)
                .IsUnique();

            // Cascade delete: deleting a Summary removes its ActionItems
            modelBuilder.Entity<Summary>()
                .HasMany(s => s.ActionItems)
                .WithOne(ai => ai.Summary)
                .HasForeignKey(ai => ai.SummaryId)
                .OnDelete(DeleteBehavior.Cascade);

            // Relationship: User → Summaries
            modelBuilder.Entity<User>()
                .HasMany(u => u.Summaries)
                .WithOne(s => s.User)
                .HasForeignKey(s => s.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}