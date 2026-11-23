using FiapVideo.Auth.Domain;
using Microsoft.EntityFrameworkCore;

namespace FiapVideo.Auth.Infrastructure;

public class AuthDbContext(DbContextOptions<AuthDbContext> options) : DbContext(options)
{
    public DbSet<Usuario> Usuarios => Set<Usuario>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Usuario>(e =>
        {
            e.ToTable("usuarios");
            e.HasKey(x => x.Id);
            e.Property(x => x.Nome).HasMaxLength(200).IsRequired();
            e.Property(x => x.Email).HasMaxLength(254).IsRequired();
            e.HasIndex(x => x.Email).IsUnique();
            e.Property(x => x.SenhaHash).HasMaxLength(100).IsRequired();
            e.Property(x => x.CriadoEm);
        });
    }
}
