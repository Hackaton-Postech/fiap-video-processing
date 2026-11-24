using FiapVideo.Videos.Domain;

namespace FiapVideo.Videos.Application;

public record EnviarVideoCommand(
    Guid UsuarioId,
    string Email,
    string NomeUsuario,
    string NomeArquivo,
    string ContentType,
    long Tamanho,
    Stream Conteudo);

public record VideoResponse(
    Guid Id,
    string NomeOriginal,
    string Status,
    string? MensagemErro,
    DateTime CriadoEm,
    DateTime AtualizadoEm,
    bool ZipDisponivel);

public record DownloadZip(Stream Conteudo, string NomeArquivo);

public static class VideoMapping
{
    public static VideoResponse ParaResponse(this Video v) =>
        new(v.Id, v.NomeOriginal, v.Status.ToString(), v.MensagemErro, v.CriadoEm, v.AtualizadoEm,
            v.Status == StatusVideo.Concluido && v.ChaveZip is not null);
}
