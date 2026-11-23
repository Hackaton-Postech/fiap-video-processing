using FiapVideo.Auth.Domain;

namespace FiapVideo.Auth.Application;

public class AuthService(IUsuarioRepository repositorio, IPasswordHasher hasher, ITokenGenerator tokens)
{
    public const int TamanhoMinimoSenha = 8;

    public async Task<UsuarioResponse> RegistrarAsync(RegistrarRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Senha) || request.Senha.Length < TamanhoMinimoSenha)
            throw new ValidacaoException($"A senha deve ter ao menos {TamanhoMinimoSenha} caracteres.");

        var email = Normalizar(request.Email);
        if (await repositorio.ObterPorEmailAsync(email, ct) is not null)
            throw new ConflitoException("E-mail já cadastrado.");

        var usuario = Usuario.Criar(request.Nome, email, hasher.Hash(request.Senha));
        await repositorio.AdicionarAsync(usuario, ct);
        return new UsuarioResponse(usuario.Id, usuario.Nome, usuario.Email);
    }

    public async Task<TokenResponse> LoginAsync(LoginRequest request, CancellationToken ct = default)
    {
        var usuario = await repositorio.ObterPorEmailAsync(Normalizar(request.Email), ct);
        if (usuario is null || !hasher.Verificar(request.Senha ?? string.Empty, usuario.SenhaHash))
            throw new CredenciaisInvalidasException();

        var token = tokens.Gerar(usuario);
        return new TokenResponse(token.Token, token.ExpiraEmUtc);
    }

    private static string Normalizar(string? email) => (email ?? string.Empty).Trim().ToLowerInvariant();
}
