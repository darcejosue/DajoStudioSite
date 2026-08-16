using DajoStudio.UpdateServer.Models;
using Microsoft.EntityFrameworkCore;

namespace DajoStudio.UpdateServer.Data
{
    public class UpdateDbContext : DbContext
    {
        public UpdateDbContext(DbContextOptions<UpdateDbContext> options) : base(options)
        {
        }

        public DbSet<UpdateRelease> Releases { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<UpdateRelease>()
                .HasIndex(r => r.Version)
                .IsUnique();

            modelBuilder.Entity<UpdateRelease>()
                .HasIndex(r => r.IsActive);
        }
    }
}
