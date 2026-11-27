using FiapVideo.Videos.Application;
using FiapVideo.Videos.Domain;
using FiapVideo.Videos.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace FiapVideo.Videos.IntegrationTests;

[Collection(PostgresCollection.Nome)]
public class VideoRepositoryTests(PostgresFixture banco)
{
    private static Video NovoVideo(Guid? usuarioId = null) =>
        Video.Criar(Guid.NewGuid(), usuarioId ?? Guid.NewGuid(), "video.mp4", "u/v/original.mp4", 1024);

    private static VideoEnviado Evento(Video v) =>
        new(v.Id, v.UsuarioId, "pedro@teste.com", "Pedro", v.ChaveOriginal, v.NomeOriginal, v.CriadoEm);

    private async Task<Video> InserirAsync(Video? video = null)
    {
        video ??= NovoVideo();
        await using var db = banco.CriarContexto();
        await new VideoRepository(db).AdicionarComEventoAsync(video, Evento(video), CancellationToken.None);
        return video;
    }

    private async Task<Video> LerAsync(Guid id)
    {
        await using var db = banco.CriarContexto();
        var video = await new VideoRepository(db).ObterPorIdAsync(id, CancellationToken.None);
        return video ?? throw new InvalidOperationException("Vídeo não encontrado no banco.");
    }

    private async Task<bool> ProcessandoAsync(Guid id)
    {
        await using var db = banco.CriarContexto();
        return await new VideoRepository(db).MarcarProcessandoAsync(id, CancellationToken.None);
    }

    private async Task<bool> ConcluidoAsync(Guid id, string chaveZip = "u/v/frames.zip")
    {
        await using var db = banco.CriarContexto();
        return await new VideoRepository(db).MarcarConcluidoAsync(id, chaveZip, CancellationToken.None);
    }

    private async Task<bool> FalhaAsync(Guid id, string? mensagem = "erro")
    {
        await using var db = banco.CriarContexto();
        return await new VideoRepository(db).MarcarFalhaAsync(id, mensagem, CancellationToken.None);
    }

    [Fact]
    public async Task AdicionarComEvento_GravaVideoPendenteEMensagemNoOutbox()
    {
        var video = await InserirAsync();

        var lido = await LerAsync(video.Id);
        Assert.Equal(StatusVideo.Pendente, lido.Status);
        Assert.Equal(video.UsuarioId, lido.UsuarioId);

        await using var db = banco.CriarContexto();
        var mensagens = await db.OutboxMessages.Where(m => m.Payload.Contains(video.Id.ToString())).ToListAsync();
        var mensagem = Assert.Single(mensagens);
        Assert.Equal("video.enviado", mensagem.Tipo);
        Assert.Null(mensagem.PublicadoEm);
    }

    [Fact]
    public async Task AdicionarComEvento_QuandoOVideoJaExiste_NaoGravaOutboxDuplicado()
    {
        var video = await InserirAsync();

        await using (var db = banco.CriarContexto())
        {
            await Assert.ThrowsAsync<DbUpdateException>(() =>
                new VideoRepository(db).AdicionarComEventoAsync(video, Evento(video), CancellationToken.None));
        }

        await using var consulta = banco.CriarContexto();
        var total = await consulta.OutboxMessages.CountAsync(m => m.Payload.Contains(video.Id.ToString()));
        Assert.Equal(1, total);
    }

    [Fact]
    public async Task MarcarProcessando_Pendente_MudaOStatusERepetidoNaoAltera()
    {
        var video = await InserirAsync();

        Assert.True(await ProcessandoAsync(video.Id));
        Assert.False(await ProcessandoAsync(video.Id));

        Assert.Equal(StatusVideo.Processando, (await LerAsync(video.Id)).Status);
    }

    [Fact]
    public async Task MarcarProcessando_VideoInexistente_RetornaFalse()
    {
        Assert.False(await ProcessandoAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task MarcarConcluido_AindaPendente_AceitaEventoQueChegouAntesDoProcessando()
    {
        var video = await InserirAsync();

        Assert.True(await ConcluidoAsync(video.Id, "u/v/frames.zip"));

        var lido = await LerAsync(video.Id);
        Assert.Equal(StatusVideo.Concluido, lido.Status);
        Assert.Equal("u/v/frames.zip", lido.ChaveZip);
        Assert.Null(lido.MensagemErro);
    }

    [Fact]
    public async Task MarcarConcluido_Repetido_NaoSobrescreveAChaveDoZip()
    {
        var video = await InserirAsync();

        Assert.True(await ConcluidoAsync(video.Id, "primeiro.zip"));
        Assert.False(await ConcluidoAsync(video.Id, "segundo.zip"));

        Assert.Equal("primeiro.zip", (await LerAsync(video.Id)).ChaveZip);
    }

    [Fact]
    public async Task EventoForaDeOrdem_ProcessandoDepoisDeConcluido_NaoReverteOStatus()
    {
        var video = await InserirAsync();

        Assert.True(await ConcluidoAsync(video.Id));
        Assert.False(await ProcessandoAsync(video.Id));

        Assert.Equal(StatusVideo.Concluido, (await LerAsync(video.Id)).Status);
    }

    [Fact]
    public async Task MarcarConcluido_ConclusoesSimultaneas_ApenasUmaVence()
    {
        var video = await InserirAsync();

        var resultados = await Task.WhenAll(
            Enumerable.Range(0, 8).Select(i => ConcluidoAsync(video.Id, $"zip-{i}.zip")));

        Assert.Single(resultados, r => r);
        var lido = await LerAsync(video.Id);
        Assert.Equal(StatusVideo.Concluido, lido.Status);
        Assert.Matches(@"^zip-\d\.zip$", lido.ChaveZip);
    }

    [Fact]
    public async Task MarcarConcluido_ChaveVazia_LancaDomainException()
    {
        await Assert.ThrowsAsync<DomainException>(() => ConcluidoAsync(Guid.NewGuid(), " "));
    }

    [Fact]
    public async Task MarcarFalha_EmProcessamento_DefineErroEMensagem()
    {
        var video = await InserirAsync();
        await ProcessandoAsync(video.Id);

        Assert.True(await FalhaAsync(video.Id, "  arquivo corrompido "));

        var lido = await LerAsync(video.Id);
        Assert.Equal(StatusVideo.Erro, lido.Status);
        Assert.Equal("arquivo corrompido", lido.MensagemErro);
    }

    [Fact]
    public async Task MarcarFalha_MensagemLonga_EhTruncadaEm1000Caracteres()
    {
        var video = await InserirAsync();

        Assert.True(await FalhaAsync(video.Id, new string('x', 1500)));

        Assert.Equal(1000, (await LerAsync(video.Id)).MensagemErro!.Length);
    }

    [Fact]
    public async Task MarcarFalha_DepoisDeConcluido_NaoAltera()
    {
        var video = await InserirAsync();
        await ConcluidoAsync(video.Id);

        Assert.False(await FalhaAsync(video.Id, "tarde demais"));

        var lido = await LerAsync(video.Id);
        Assert.Equal(StatusVideo.Concluido, lido.Status);
        Assert.Null(lido.MensagemErro);
    }

    [Fact]
    public async Task ListarPorUsuario_RetornaSoOsDoUsuarioDoMaisNovoParaOMaisAntigo()
    {
        var usuario = Guid.NewGuid();
        var antigo = await InserirAsync(NovoVideo(usuario));
        await Task.Delay(30);
        var novo = await InserirAsync(NovoVideo(usuario));
        await InserirAsync(NovoVideo());

        await using var db = banco.CriarContexto();
        var lista = await new VideoRepository(db).ListarPorUsuarioAsync(usuario, CancellationToken.None);

        Assert.Equal(new[] { novo.Id, antigo.Id }, lista.Select(v => v.Id));
    }
}
