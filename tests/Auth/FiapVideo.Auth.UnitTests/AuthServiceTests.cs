using FiapVideo.Auth.Application;
using FiapVideo.Auth.Domain;
using NSubstitute;

namespace FiapVideo.Auth.UnitTests;

public class AuthServiceTests
{
    private readonly IUsuarioRepository _repo = Substitute.For<IUsuarioRepository>();
    private readonly IPasswordHasher _hasher = Substitute.For<IPasswordHasher>();
    private readonly ITokenGenerator _tokens = Substitute.For<ITokenGenerator>();
    private readonly AuthService _sut;

    public AuthServiceTests()
    {
        _sut = new AuthService(_repo, _hasher, _tokens);
    }

    [Theory]
    [InlineData("")]
    [InlineData("curta12")]
    public async Task Registrar_SenhaCurta_LancaValidacao(string senha)
    {
        var req = new RegistrarRequest("Pedro", "pedro@teste.com", senha);

        await Assert.ThrowsAsync<ValidacaoException>(() => _sut.RegistrarAsync(req));
        await _repo.DidNotReceive().AdicionarAsync(Arg.Any<Usuario>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Registrar_EmailJaCadastrado_LancaConflito()
    {
        var existente = Usuario.Criar("Pedro", "pedro@teste.com", "hash");
        _repo.ObterPorEmailAsync("pedro@teste.com", Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<Usuario?>(existente));

        var req = new RegistrarRequest("Pedro", "pedro@teste.com", "Senha12345");

        await Assert.ThrowsAsync<ConflitoException>(() => _sut.RegistrarAsync(req));
        await _repo.DidNotReceive().AdicionarAsync(Arg.Any<Usuario>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Registrar_Valido_NormalizaEmailEGravaSenhaHasheada()
    {
        _repo.ObterPorEmailAsync("pedro@teste.com", Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<Usuario?>(null));
        _hasher.Hash("Senha12345").Returns("hash-gerado");

        var resposta = await _sut.RegistrarAsync(new RegistrarRequest("Pedro", "  PEDRO@teste.com ", "Senha12345"));

        Assert.Equal("pedro@teste.com", resposta.Email);
        Assert.Equal("Pedro", resposta.Nome);
        await _repo.Received(1).AdicionarAsync(
            Arg.Is<Usuario>(u => u.Email == "pedro@teste.com" && u.SenhaHash == "hash-gerado"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Login_UsuarioInexistente_LancaCredenciaisInvalidas()
    {
        _repo.ObterPorEmailAsync("x@teste.com", Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<Usuario?>(null));

        await Assert.ThrowsAsync<CredenciaisInvalidasException>(
            () => _sut.LoginAsync(new LoginRequest("x@teste.com", "Senha12345")));
    }

    [Fact]
    public async Task Login_SenhaIncorreta_LancaCredenciaisInvalidas()
    {
        var usuario = Usuario.Criar("Pedro", "pedro@teste.com", "hash");
        _repo.ObterPorEmailAsync("pedro@teste.com", Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<Usuario?>(usuario));
        _hasher.Verificar("errada123", "hash").Returns(false);

        await Assert.ThrowsAsync<CredenciaisInvalidasException>(
            () => _sut.LoginAsync(new LoginRequest("pedro@teste.com", "errada123")));
        _tokens.DidNotReceive().Gerar(Arg.Any<Usuario>());
    }

    [Fact]
    public async Task Login_Valido_RetornaToken()
    {
        var usuario = Usuario.Criar("Pedro", "pedro@teste.com", "hash");
        var expira = DateTime.UtcNow.AddHours(1);
        _repo.ObterPorEmailAsync("pedro@teste.com", Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<Usuario?>(usuario));
        _hasher.Verificar("Senha12345", "hash").Returns(true);
        _tokens.Gerar(usuario).Returns(new TokenGerado("jwt-de-teste", expira));

        var resposta = await _sut.LoginAsync(new LoginRequest(" PEDRO@teste.com", "Senha12345"));

        Assert.Equal("jwt-de-teste", resposta.Token);
        Assert.Equal(expira, resposta.ExpiraEmUtc);
    }
}
