using FiapVideo.Videos.Domain;
using Microsoft.EntityFrameworkCore;

namespace FiapVideo.Videos.Infrastructure;

public class VideosDbContext(DbContextOptions<VideosDbContext> options) : DbContext(options)
{
    public DbSet<Video> Videos => Set<Video>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Video>(e =>
        {
            e.ToTable("videos");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).ValueGeneratedNever();
            e.Property(x => x.UsuarioId).IsRequired();
            e.Property(x => x.NomeOriginal).HasMaxLength(255).IsRequired();
            e.Property(x => x.ChaveOriginal).HasMaxLength(500).IsRequired();
            e.Property(x => x.ChaveZip).HasMaxLength(500);
            e.Property(x => x.TamanhoBytes);
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
            e.Property(x => x.MensagemErro).HasMaxLength(1000);
            e.Property(x => x.CriadoEm);
            e.Property(x => x.AtualizadoEm);
            e.HasIndex(x => new { x.UsuarioId, x.CriadoEm });
        });

        modelBuilder.Entity<OutboxMessage>(e =>
        {
            e.ToTable("outbox_messages");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).ValueGeneratedNever();
            e.Property(x => x.Tipo).HasMaxLength(100).IsRequired();
            e.Property(x => x.Payload).IsRequired();
            e.Property(x => x.UltimoErro).HasMaxLength(1000);
            e.HasIndex(x => x.PublicadoEm);
        });
    }
}
