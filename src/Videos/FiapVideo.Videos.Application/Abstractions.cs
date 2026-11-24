using FiapVideo.Videos.Domain;

namespace FiapVideo.Videos.Application;

public interface IVideoRepository
{
    // Grava o vídeo e a mensagem do outbox na mesma transação.
    Task AdicionarComEventoAsync(Video video, VideoEnviado evento, CancellationToken ct);
    Task<Video?> ObterPorIdAsync(Guid id, CancellationToken ct);
    Task<IReadOnlyList<Video>> ListarPorUsuarioAsync(Guid usuarioId, CancellationToken ct);
}

public interface IVideoStorage
{
    Task EnviarAsync(string chave, Stream conteudo, string contentType, CancellationToken ct);
    Task<Stream> BaixarAsync(string chave, CancellationToken ct);
}
