using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using FiapVideo.Auth.Application;
using FiapVideo.Auth.Domain;
using Microsoft.IdentityModel.Tokens;

namespace FiapVideo.Auth.Infrastructure;

public class JwtTokenGenerator(JwtOptions options) : ITokenGenerator
{
    public TokenGerado Gerar(Usuario usuario)
    {
        var chave = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(options.Secret));
        var credenciais = new SigningCredentials(chave, SecurityAlgorithms.HmacSha256);
        var expira = DateTime.UtcNow.AddMinutes(options.ExpiraEmMinutos);

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, usuario.Id.ToString()),
            new Claim(JwtRegisteredClaimNames.Email, usuario.Email),
            new Claim(JwtRegisteredClaimNames.Name, usuario.Nome),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        var jwt = new JwtSecurityToken(options.Issuer, options.Audience, claims,
            expires: expira, signingCredentials: credenciais);

        return new TokenGerado(new JwtSecurityTokenHandler().WriteToken(jwt), expira);
    }
}
