using FiapVideo.Auth.Application;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FiapVideo.Auth.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddAuthInfrastructure(
        this IServiceCollection services, string connectionString, JwtOptions jwt)
    {
        if (string.IsNullOrWhiteSpace(jwt.Secret) || jwt.Secret.Length < 32)
            throw new InvalidOperationException("Jwt:Secret deve ter ao menos 32 caracteres.");

        services.AddSingleton(jwt);
        services.AddDbContext<AuthDbContext>(o => o.UseNpgsql(connectionString));
        services.AddScoped<IUsuarioRepository, UsuarioRepository>();
        services.AddSingleton<IPasswordHasher, BcryptPasswordHasher>();
        services.AddSingleton<ITokenGenerator, JwtTokenGenerator>();
        services.AddScoped<AuthService>();
        return services;
    }
}
