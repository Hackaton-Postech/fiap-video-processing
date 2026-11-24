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
