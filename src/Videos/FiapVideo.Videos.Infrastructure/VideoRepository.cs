using System.Text.Json;
using FiapVideo.Videos.Application;
using FiapVideo.Videos.Domain;
using Microsoft.EntityFrameworkCore;

namespace FiapVideo.Videos.Infrastructure;

public class VideoRepository(VideosDbContext db) : IVideoRepository
{
    public async Task AdicionarComEventoAsync(Video video, VideoEnviado evento, CancellationToken ct)
    {
        db.Videos.Add(video);
        db.OutboxMessages.Add(OutboxMessage.Criar(
            RoutingKeys.VideoEnviado,
            JsonSerializer.Serialize(evento, OutboxJson.Options)));

        // Um único SaveChanges grava o vídeo e a mensagem na mesma transação.
        await db.SaveChangesAsync(ct);
    }

    public Task<Video?> ObterPorIdAsync(Guid id, CancellationToken ct) =>
        db.Videos.AsNoTracking().FirstOrDefaultAsync(v => v.Id == id, ct);

    public async Task<IReadOnlyList<Video>> ListarPorUsuarioAsync(Guid usuarioId, CancellationToken ct) =>
        await db.Videos.AsNoTracking()
            .Where(v => v.UsuarioId == usuarioId)
            .OrderByDescending(v => v.CriadoEm)
            .ToListAsync(ct);
}
