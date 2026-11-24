using FiapVideo.Videos.Domain;

namespace FiapVideo.Videos.Application;

public class VideoService(IVideoRepository repositorio, IVideoStorage storage)
{
    public const long TamanhoMaximoBytes = 200L * 1024 * 1024;

    public static readonly IReadOnlySet<string> ExtensoesPermitidas =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".mp4", ".avi", ".mov", ".mkv", ".webm" };

    public async Task<VideoResponse> EnviarAsync(EnviarVideoCommand cmd, CancellationToken ct = default)
    {
        var nome = Path.GetFileName(cmd.NomeArquivo ?? string.Empty).Trim();
        if (nome.Length == 0)
            throw new ValidacaoException("Nome do arquivo é obrigatório.");
        if (nome.Length > 255)
            nome = nome[^255..];

        var extensao = Path.GetExtension(nome);
        if (!ExtensoesPermitidas.Contains(extensao))
            throw new ValidacaoException(
                $"Formato não suportado. Use: {string.Join(", ", ExtensoesPermitidas.Order())}.");
        if (cmd.Tamanho <= 0)
            throw new ValidacaoException("O arquivo está vazio.");
        if (cmd.Tamanho > TamanhoMaximoBytes)
            throw new ValidacaoException(
                $"O arquivo excede o limite de {TamanhoMaximoBytes / (1024 * 1024)} MB.");

        var id = Guid.NewGuid();
        var chave = $"{cmd.UsuarioId}/{id}/original{extensao.ToLowerInvariant()}";
        var video = Video.Criar(id, cmd.UsuarioId, nome, chave, cmd.Tamanho);

        await storage.EnviarAsync(chave, cmd.Conteudo, cmd.ContentType, ct);

        var evento = new VideoEnviado(id, cmd.UsuarioId, cmd.Email, cmd.NomeUsuario, chave, nome, video.CriadoEm);
        await repositorio.AdicionarComEventoAsync(video, evento, ct);

        return video.ParaResponse();
    }

    public async Task<IReadOnlyList<VideoResponse>> ListarAsync(Guid usuarioId, CancellationToken ct = default)
    {
        var videos = await repositorio.ListarPorUsuarioAsync(usuarioId, ct);
        return videos.Select(v => v.ParaResponse()).ToList();
    }

    public async Task<VideoResponse> ObterAsync(Guid usuarioId, Guid videoId, CancellationToken ct = default)
    {
        var video = await repositorio.ObterPorIdAsync(videoId, ct);
        if (video is null || video.UsuarioId != usuarioId)
            throw new NaoEncontradoException("Vídeo não encontrado.");
        return video.ParaResponse();
    }

    public async Task<DownloadZip> BaixarZipAsync(Guid usuarioId, Guid videoId, CancellationToken ct = default)
    {
        var video = await repositorio.ObterPorIdAsync(videoId, ct);
        if (video is null || video.UsuarioId != usuarioId)
            throw new NaoEncontradoException("Vídeo não encontrado.");
        if (video.Status != StatusVideo.Concluido || video.ChaveZip is null)
            throw new ConflitoException("O processamento ainda não foi concluído.");

        var conteudo = await storage.BaixarAsync(video.ChaveZip, ct);
        return new DownloadZip(conteudo, $"{Path.GetFileNameWithoutExtension(video.NomeOriginal)}.zip");
    }
}
