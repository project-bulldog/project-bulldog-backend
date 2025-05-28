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
        public DbSet<Reminder> Reminders { get; set; }

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

            // Relationship: User -> Summaries
            modelBuilder.Entity<User>()
                .HasMany(u => u.Summaries)
                .WithOne(s => s.User)
                .HasForeignKey(s => s.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            // Relationship: User -> Reminders
            modelBuilder.Entity<Reminder>(entity =>
                {
                    entity.HasKey(r => r.Id);

                    // Relationships
                    entity.HasOne(r => r.User)
                          .WithMany()
                          .HasForeignKey(r => r.UserId)
                          .OnDelete(DeleteBehavior.Cascade); // Delete reminders if user is deleted

                    entity.HasOne(r => r.ActionItem)
                          .WithMany()
                          .HasForeignKey(r => r.ActionItemId)
                          .OnDelete(DeleteBehavior.SetNull);

                    // Properties
                    entity.Property(r => r.Message)
                          .IsRequired()
                          .HasMaxLength(500);

                    entity.Property(r => r.ReminderTime)
                          .IsRequired();

                    entity.Property(r => r.IsSent)
                          .HasDefaultValue(false);
                });

            modelBuilder.Entity<ActionItem>(entity =>
            {
                entity.HasKey(ai => ai.Id);

                entity.Property(ai => ai.Id)
                      .ValueGeneratedNever(); // ✅ Prevent PostgreSQL identity error

                entity.Property(ai => ai.Text)
                      .IsRequired()
                      .HasMaxLength(500);

                entity.Property(ai => ai.IsDone)
                      .IsRequired();

                entity.HasOne(ai => ai.Summary)
                      .WithMany(s => s.ActionItems)
                      .HasForeignKey(ai => ai.SummaryId)
                      .OnDelete(DeleteBehavior.Cascade);
            });
        }
    }
}
