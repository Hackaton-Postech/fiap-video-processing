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

    // As três transições abaixo espelham as regras do domínio (Video.IniciarProcessamento, Concluir e Falhar),
    // mas rodam como UPDATE condicional: são atômicas e idempotentes mesmo com várias réplicas consumindo.

    public async Task<bool> MarcarProcessandoAsync(Guid videoId, CancellationToken ct)
    {
        var agora = DateTime.UtcNow;
        var linhas = await db.Videos
            .Where(v => v.Id == videoId && v.Status == StatusVideo.Pendente)
            .ExecuteUpdateAsync(s => s
                .SetProperty(v => v.Status, StatusVideo.Processando)
                .SetProperty(v => v.AtualizadoEm, agora), ct);
        return linhas > 0;
    }

    public async Task<bool> MarcarConcluidoAsync(Guid videoId, string chaveZip, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(chaveZip))
            throw new DomainException("Chave do zip é obrigatória.");

        var agora = DateTime.UtcNow;
        var linhas = await db.Videos
            .Where(v => v.Id == videoId &&
                        (v.Status == StatusVideo.Pendente || v.Status == StatusVideo.Processando))
            .ExecuteUpdateAsync(s => s
                .SetProperty(v => v.Status, StatusVideo.Concluido)
                .SetProperty(v => v.ChaveZip, chaveZip)
                .SetProperty(v => v.MensagemErro, (string?)null)
                .SetProperty(v => v.AtualizadoEm, agora), ct);
        return linhas > 0;
    }

    public async Task<bool> MarcarFalhaAsync(Guid videoId, string? mensagem, CancellationToken ct)
    {
        var texto = string.IsNullOrWhiteSpace(mensagem) ? "Erro no processamento." : mensagem.Trim();
        if (texto.Length > 1000) texto = texto[..1000];

        var agora = DateTime.UtcNow;
        var linhas = await db.Videos
            .Where(v => v.Id == videoId &&
                        (v.Status == StatusVideo.Pendente || v.Status == StatusVideo.Processando))
            .ExecuteUpdateAsync(s => s
                .SetProperty(v => v.Status, StatusVideo.Erro)
                .SetProperty(v => v.MensagemErro, texto)
                .SetProperty(v => v.AtualizadoEm, agora), ct);
        return linhas > 0;
    }
}
