using FiapVideo.Videos.Domain;

namespace FiapVideo.Videos.Application;

public interface IVideoRepository
{
    // Grava o vídeo e a mensagem do outbox na mesma transação.
    Task AdicionarComEventoAsync(Video video, VideoEnviado evento, CancellationToken ct);
    Task<Video?> ObterPorIdAsync(Guid id, CancellationToken ct);
    Task<IReadOnlyList<Video>> ListarPorUsuarioAsync(Guid usuarioId, CancellationToken ct);

    // Transições de status. Devolvem false quando nada mudou (evento repetido, fora de ordem ou vídeo inexistente).
    Task<bool> MarcarProcessandoAsync(Guid videoId, CancellationToken ct);
    Task<bool> MarcarConcluidoAsync(Guid videoId, string chaveZip, CancellationToken ct);
    Task<bool> MarcarFalhaAsync(Guid videoId, string? mensagem, CancellationToken ct);
}

public interface IVideoStorage
{
    Task EnviarAsync(string chave, Stream conteudo, string contentType, CancellationToken ct);
    Task<Stream> BaixarAsync(string chave, CancellationToken ct);
}
