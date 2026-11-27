namespace FiapVideo.Worker.UnitTests;

public class VideoProcessorTests
{
    private readonly FakeStorage _storage = new();
    private readonly FakeExtractor _extrator = new();
    private readonly VideoProcessor _sut;

    public VideoProcessorTests()
    {
        _sut = new VideoProcessor(_storage, _extrator);
    }

    private static VideoEnviado Mensagem(string chave = "u1/v1/original.mp4") =>
        new(Guid.NewGuid(), Guid.NewGuid(), "pedro@teste.com", "Pedro", chave, "video.mp4", DateTime.UtcNow);

    [Theory]
    [InlineData("u1/v1/original.mp4", "u1/v1/frames.zip")]
    [InlineData("original.mp4", "frames.zip")]
    [InlineData("a/b/c/d/original.mov", "a/b/c/d/frames.zip")]
    public void ChaveDoZip_TrocaOArquivoPeloZipNaMesmaPasta(string original, string esperado)
    {
        Assert.Equal(esperado, VideoProcessor.ChaveDoZip(original));
    }

    [Fact]
    public async Task Processar_Sucesso_RetornaChaveDoZipETotalDeFrames()
    {
        _extrator.Frames = 3;
        var mensagem = Mensagem();

        var resultado = await _sut.ProcessarAsync(mensagem, CancellationToken.None);

        Assert.Equal("u1/v1/frames.zip", resultado.ChaveZip);
        Assert.Equal(3, resultado.TotalFrames);
        Assert.Equal(mensagem.ChaveOriginal, _storage.ChaveBaixada);
    }

    [Fact]
    public async Task Processar_Sucesso_EnviaOZipComTodosOsFramesAoStorage()
    {
        _extrator.Frames = 4;

        await _sut.ProcessarAsync(Mensagem(), CancellationToken.None);

        Assert.Equal("u1/v1/frames.zip", _storage.ChaveEnviada);
        Assert.Equal("application/zip", _storage.ContentTypeEnviado);
        Assert.Equal(4, _storage.EntradasDoZip.Count);
        Assert.All(_storage.EntradasDoZip, nome => Assert.StartsWith("frame_", nome, StringComparison.Ordinal));
    }

    [Fact]
    public async Task Processar_MantemAExtensaoDoArquivoOriginal()
    {
        await _sut.ProcessarAsync(Mensagem("u1/v1/original.mkv"), CancellationToken.None);

        Assert.EndsWith(".mkv", _storage.ArquivoBaixado, StringComparison.Ordinal);
        Assert.Equal(_storage.ArquivoBaixado, _extrator.VideoRecebido);
    }

    [Fact]
    public async Task Processar_Sucesso_ApagaAPastaTemporaria()
    {
        await _sut.ProcessarAsync(Mensagem(), CancellationToken.None);

        var pasta = Path.GetDirectoryName(_storage.ArquivoBaixado)!;
        Assert.False(Directory.Exists(pasta));
    }

    [Fact]
    public async Task Processar_VideoInvalido_PropagaOErroNaoEnviaZipEApagaAPasta()
    {
        _extrator.Erro = new ProcessamentoException("O arquivo enviado não é um vídeo válido.");

        var erro = await Assert.ThrowsAsync<ProcessamentoException>(
            () => _sut.ProcessarAsync(Mensagem(), CancellationToken.None));

        Assert.Contains("vídeo válido", erro.Message);
        Assert.Null(_storage.ChaveEnviada);
        Assert.False(Directory.Exists(Path.GetDirectoryName(_storage.ArquivoBaixado)!));
    }

    [Fact]
    public async Task Processar_ArquivoInexistenteNoStorage_PropagaOErroSemExtrairFrames()
    {
        _storage.ErroAoBaixar = new ProcessamentoException("Arquivo original não encontrado no storage.");

        await Assert.ThrowsAsync<ProcessamentoException>(
            () => _sut.ProcessarAsync(Mensagem(), CancellationToken.None));

        Assert.Null(_extrator.VideoRecebido);
        Assert.Null(_storage.ChaveEnviada);
    }
}
