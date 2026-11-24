using FiapVideo.Videos.Application;
using FiapVideo.Videos.Domain;
using NSubstitute;

namespace FiapVideo.Videos.UnitTests;

public class VideoServiceTests
{
    private readonly IVideoRepository _repo = Substitute.For<IVideoRepository>();
    private readonly IVideoStorage _storage = Substitute.For<IVideoStorage>();
    private readonly VideoService _sut;

    public VideoServiceTests()
    {
        _sut = new VideoService(_repo, _storage);
    }

    private static EnviarVideoCommand Comando(string nome = "video.mp4", long tamanho = 3, Guid? usuarioId = null) =>
        new(usuarioId ?? Guid.NewGuid(), "pedro@teste.com", "Pedro", nome, "video/mp4", tamanho,
            new MemoryStream(new byte[] { 1, 2, 3 }));

    private async Task AssertNaoGravouAsync()
    {
        await _storage.DidNotReceive().EnviarAsync(
            Arg.Any<string>(), Arg.Any<Stream>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
        await _repo.DidNotReceive().AdicionarComEventoAsync(
            Arg.Any<Video>(), Arg.Any<VideoEnviado>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Enviar_Valido_GravaNoStorageERepositorioComEvento()
    {
        var usuarioId = Guid.NewGuid();
        var cmd = Comando(usuarioId: usuarioId);

        var resposta = await _sut.EnviarAsync(cmd);

        Assert.Equal("Pendente", resposta.Status);
        Assert.False(resposta.ZipDisponivel);
        await _storage.Received(1).EnviarAsync(
            Arg.Is<string>(c => c.StartsWith(usuarioId + "/") && c.EndsWith("/original.mp4")),
            cmd.Conteudo, "video/mp4", Arg.Any<CancellationToken>());
        await _repo.Received(1).AdicionarComEventoAsync(
            Arg.Is<Video>(v => v.UsuarioId == usuarioId && v.Status == StatusVideo.Pendente),
            Arg.Is<VideoEnviado>(e => e.VideoId == resposta.Id && e.Email == "pedro@teste.com"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Enviar_ExtensaoEmMaiusculas_EhAceitaEChaveFicaEmMinusculas()
    {
        var cmd = Comando("VIDEO.MP4");

        await _sut.EnviarAsync(cmd);

        await _storage.Received(1).EnviarAsync(
            Arg.Is<string>(c => c.EndsWith("/original.mp4")),
            Arg.Any<Stream>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Enviar_NomeComCaminho_UsaApenasONomeDoArquivo()
    {
        var resposta = await _sut.EnviarAsync(Comando("../../video.mp4"));

        Assert.Equal("video.mp4", resposta.NomeOriginal);
    }

    [Fact]
    public async Task Enviar_ExtensaoNaoSuportada_LancaValidacaoENaoGrava()
    {
        await Assert.ThrowsAsync<ValidacaoException>(() => _sut.EnviarAsync(Comando("documento.txt")));

        await AssertNaoGravouAsync();
    }

    [Fact]
    public async Task Enviar_NomeVazio_LancaValidacaoENaoGrava()
    {
        await Assert.ThrowsAsync<ValidacaoException>(() => _sut.EnviarAsync(Comando("")));

        await AssertNaoGravouAsync();
    }

    [Fact]
    public async Task Enviar_ArquivoVazio_LancaValidacaoENaoGrava()
    {
        await Assert.ThrowsAsync<ValidacaoException>(() => _sut.EnviarAsync(Comando(tamanho: 0)));

        await AssertNaoGravouAsync();
    }

    [Fact]
    public async Task Enviar_ArquivoMuitoGrande_LancaValidacaoENaoGrava()
    {
        var cmd = Comando(tamanho: VideoService.TamanhoMaximoBytes + 1);

        await Assert.ThrowsAsync<ValidacaoException>(() => _sut.EnviarAsync(cmd));

        await AssertNaoGravouAsync();
    }

    [Fact]
    public async Task Listar_RetornaVideosDoUsuarioNaOrdemDoRepositorio()
    {
        var usuarioId = Guid.NewGuid();
        var v1 = Video.Criar(Guid.NewGuid(), usuarioId, "a.mp4", "k1", 10);
        var v2 = Video.Criar(Guid.NewGuid(), usuarioId, "b.mp4", "k2", 10);
        _repo.ListarPorUsuarioAsync(usuarioId, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<Video>>(new List<Video> { v1, v2 }));

        var lista = await _sut.ListarAsync(usuarioId);

        Assert.Equal(new[] { v1.Id, v2.Id }, lista.Select(x => x.Id));
    }

    [Fact]
    public async Task Obter_DoProprioUsuario_RetornaVideo()
    {
        var usuarioId = Guid.NewGuid();
        var video = Video.Criar(Guid.NewGuid(), usuarioId, "a.mp4", "k1", 10);
        _repo.ObterPorIdAsync(video.Id, Arg.Any<CancellationToken>()).Returns(Task.FromResult<Video?>(video));

        var resposta = await _sut.ObterAsync(usuarioId, video.Id);

        Assert.Equal(video.Id, resposta.Id);
    }

    [Fact]
    public async Task Obter_DeOutroUsuario_LancaNaoEncontrado()
    {
        var video = Video.Criar(Guid.NewGuid(), Guid.NewGuid(), "a.mp4", "k1", 10);
        _repo.ObterPorIdAsync(video.Id, Arg.Any<CancellationToken>()).Returns(Task.FromResult<Video?>(video));

        await Assert.ThrowsAsync<NaoEncontradoException>(() => _sut.ObterAsync(Guid.NewGuid(), video.Id));
    }

    [Fact]
    public async Task Obter_Inexistente_LancaNaoEncontrado()
    {
        var id = Guid.NewGuid();
        _repo.ObterPorIdAsync(id, Arg.Any<CancellationToken>()).Returns(Task.FromResult<Video?>(null));

        await Assert.ThrowsAsync<NaoEncontradoException>(() => _sut.ObterAsync(Guid.NewGuid(), id));
    }

    [Fact]
    public async Task Baixar_AindaPendente_LancaConflito()
    {
        var usuarioId = Guid.NewGuid();
        var video = Video.Criar(Guid.NewGuid(), usuarioId, "a.mp4", "k1", 10);
        _repo.ObterPorIdAsync(video.Id, Arg.Any<CancellationToken>()).Returns(Task.FromResult<Video?>(video));

        await Assert.ThrowsAsync<ConflitoException>(() => _sut.BaixarZipAsync(usuarioId, video.Id));
    }

    [Fact]
    public async Task Baixar_DeOutroUsuario_LancaNaoEncontrado()
    {
        var video = Video.Criar(Guid.NewGuid(), Guid.NewGuid(), "a.mp4", "k1", 10);
        video.Concluir("k1.zip");
        _repo.ObterPorIdAsync(video.Id, Arg.Any<CancellationToken>()).Returns(Task.FromResult<Video?>(video));

        await Assert.ThrowsAsync<NaoEncontradoException>(() => _sut.BaixarZipAsync(Guid.NewGuid(), video.Id));
    }

    [Fact]
    public async Task Baixar_Concluido_RetornaStreamENomeDoZip()
    {
        var usuarioId = Guid.NewGuid();
        var video = Video.Criar(Guid.NewGuid(), usuarioId, "ferias.mp4", "u/v/original.mp4", 10);
        video.Concluir("u/v/frames.zip");
        var stream = new MemoryStream(new byte[] { 9 });
        _repo.ObterPorIdAsync(video.Id, Arg.Any<CancellationToken>()).Returns(Task.FromResult<Video?>(video));
        _storage.BaixarAsync("u/v/frames.zip", Arg.Any<CancellationToken>()).Returns(Task.FromResult<Stream>(stream));

        var resultado = await _sut.BaixarZipAsync(usuarioId, video.Id);

        Assert.Same(stream, resultado.Conteudo);
        Assert.Equal("ferias.zip", resultado.NomeArquivo);
    }
}
