using FiapVideo.Auth.Application;
using FiapVideo.Auth.Domain;
using FiapVideo.Auth.Infrastructure;

namespace FiapVideo.Auth.IntegrationTests;

[Collection(PostgresCollection.Nome)]
public class AuthIntegrationTests(PostgresFixture banco)
{
    private static readonly JwtOptions Jwt = new() { Secret = "chave-de-teste-com-mais-de-32-caracteres-123" };

    private static string EmailUnico() => $"{Guid.NewGuid():N}@teste.com";

    private static AuthService NovoServico(AuthDbContext db) =>
        new(new UsuarioRepository(db), new BcryptPasswordHasher(), new JwtTokenGenerator(Jwt));

    private async Task AdicionarAsync(Usuario usuario)
    {
        await using var db = banco.CriarContexto();
        await new UsuarioRepository(db).AdicionarAsync(usuario, CancellationToken.None);
    }

    private async Task<Usuario?> ObterAsync(string email)
    {
        await using var db = banco.CriarContexto();
        return await new UsuarioRepository(db).ObterPorEmailAsync(email, CancellationToken.None);
    }

    [Fact]
    public async Task Adicionar_DepoisObterPorEmail_RetornaOUsuarioGravado()
    {
        var email = EmailUnico();
        var usuario = Usuario.Criar("Pedro", email, "hash");

        await AdicionarAsync(usuario);

        var lido = await ObterAsync(email);
        Assert.NotNull(lido);
        Assert.Equal(usuario.Id, lido!.Id);
        Assert.Equal("Pedro", lido.Nome);
    }

    [Fact]
    public async Task ObterPorEmail_Inexistente_RetornaNull()
    {
        Assert.Null(await ObterAsync(EmailUnico()));
    }

    [Fact]
    public async Task Adicionar_EmailDuplicado_LancaConflito()
    {
        var email = EmailUnico();
        await AdicionarAsync(Usuario.Criar("Pedro", email, "hash"));

        await Assert.ThrowsAsync<ConflitoException>(
            () => AdicionarAsync(Usuario.Criar("Outro", email, "hash")));
    }

    [Fact]
    public async Task Adicionar_EmailComCaixaDiferente_LancaConflito()
    {
        var email = EmailUnico();
        await AdicionarAsync(Usuario.Criar("Pedro", email.ToLowerInvariant(), "hash"));

        await Assert.ThrowsAsync<ConflitoException>(
            () => AdicionarAsync(Usuario.Criar("Outro", email.ToUpperInvariant(), "hash")));
    }

    [Fact]
    public async Task RegistrarELogar_ComBancoReal_RetornaToken()
    {
        var email = EmailUnico();
        await using (var db = banco.CriarContexto())
            await NovoServico(db).RegistrarAsync(new RegistrarRequest("Pedro", email, "Senha12345"));

        await using var db2 = banco.CriarContexto();
        var token = await NovoServico(db2).LoginAsync(new LoginRequest(email, "Senha12345"));

        Assert.False(string.IsNullOrWhiteSpace(token.Token));
    }

    [Fact]
    public async Task Login_SenhaErrada_LancaCredenciaisInvalidas()
    {
        var email = EmailUnico();
        await using (var db = banco.CriarContexto())
            await NovoServico(db).RegistrarAsync(new RegistrarRequest("Pedro", email, "Senha12345"));

        await using var db2 = banco.CriarContexto();
        await Assert.ThrowsAsync<CredenciaisInvalidasException>(
            () => NovoServico(db2).LoginAsync(new LoginRequest(email, "errada123")));
    }

    [Fact]
    public async Task Registrar_EmailJaCadastrado_LancaConflito()
    {
        var email = EmailUnico();
        await using (var db = banco.CriarContexto())
            await NovoServico(db).RegistrarAsync(new RegistrarRequest("Pedro", email, "Senha12345"));

        await using var db2 = banco.CriarContexto();
        await Assert.ThrowsAsync<ConflitoException>(
            () => NovoServico(db2).RegistrarAsync(new RegistrarRequest("Outro", email, "Senha12345")));
    }
}
