using Microsoft.EntityFrameworkCore;
using StackFlow.Models;
using System.Collections.Generic;
using System.Reflection.Emit;


namespace StackFlow.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options)
            : base(options)
        {
        }

        public DbSet<User> Users { get; set; }
        public DbSet<Role> Roles { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<User>()
                .ToTable("users");

            modelBuilder.Entity<Role>()
                .ToTable("roles");

            base.OnModelCreating(modelBuilder);
        }
    }
}
