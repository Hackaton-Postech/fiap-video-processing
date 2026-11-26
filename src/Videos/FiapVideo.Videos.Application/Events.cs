namespace FiapVideo.Videos.Application;

// Mensagem publicada na fila (routing key "video.enviado").
public record VideoEnviado(
    Guid VideoId,
    Guid UsuarioId,
    string Email,
    string Nome,
    string ChaveOriginal,
    string NomeOriginal,
    DateTime EnviadoEmUtc);

// Eventos publicados pelo Worker e consumidos pela Video API.
public record VideoProcessando(Guid VideoId, DateTime EmUtc);

public record VideoProcessado(
    Guid VideoId, Guid UsuarioId, string Email, string Nome, string NomeOriginal,
    string ChaveZip, int TotalFrames, DateTime EmUtc);

public record VideoFalhou(
    Guid VideoId, Guid UsuarioId, string Email, string Nome, string NomeOriginal,
    string Mensagem, DateTime EmUtc);
