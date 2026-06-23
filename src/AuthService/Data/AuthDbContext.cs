using AuthService.Models;
using Microsoft.EntityFrameworkCore;

namespace AuthService.Data;

public class AuthDbContext(DbContextOptions<AuthDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();

    protected override void OnModelCreating(ModelBuilder model)
    {
        model.Entity<User>(e =>
        {
            e.ToTable("users");
            e.HasKey(u => u.Id);
            e.Property<object>(u => u.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
            e.Property(u => u.Username).HasColumnName("username").HasMaxLength(64).IsRequired();
            e.Property(u => u.PasswordHash).HasColumnName("password_hash").HasMaxLength(256).IsRequired();
            e.Property(u => u.Role).HasColumnName("role").HasMaxLength(32).HasDefaultValue("OPERATOR");
            e.Property<object>(u => u.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("now()");
            e.HasIndex(u => u.Username).IsUnique();
        });
    }
}