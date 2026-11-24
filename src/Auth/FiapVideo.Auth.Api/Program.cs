using System.Security.Claims;
using System.Text;
using FiapVideo.Auth.Application;
using FiapVideo.Auth.Domain;
using FiapVideo.Auth.Infrastructure;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("Auth");
if (string.IsNullOrWhiteSpace(connectionString))
    throw new InvalidOperationException("ConnectionStrings:Auth não configurada.");

var jwt = builder.Configuration.GetSection("Jwt").Get<JwtOptions>() ?? new JwtOptions();

builder.Services.AddAuthInfrastructure(connectionString, jwt);

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(o =>
    {
        o.MapInboundClaims = false;
        o.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwt.Issuer,
            ValidateAudience = true,
            ValidAudience = jwt.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Secret)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30)
        };
    });
builder.Services.AddAuthorization();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddHealthChecks()
    .AddDbContextCheck<AuthDbContext>("db", tags: new[] { "ready" });

var app = builder.Build();

app.Use(async (ctx, next) =>
{
    try
    {
        await next();
    }
    catch (Exception ex) when (ex is ValidacaoException or DomainException or ConflitoException or CredenciaisInvalidasException)
    {
        var (status, code) = ex switch
        {
            ConflitoException => (StatusCodes.Status409Conflict, "conflito"),
            CredenciaisInvalidasException => (StatusCodes.Status401Unauthorized, "credenciais_invalidas"),
            _ => (StatusCodes.Status400BadRequest, "validacao")
        };
        ctx.Response.StatusCode = status;
        await ctx.Response.WriteAsJsonAsync(new { code, message = ex.Message });
    }
});

app.UseSwagger();
app.UseSwaggerUI();

app.UseAuthentication();
app.UseAuthorization();

var auth = app.MapGroup("/auth");

auth.MapPost("/register", async (RegistrarRequest req, AuthService svc, CancellationToken ct) =>
{
    var usuario = await svc.RegistrarAsync(req, ct);
    return Results.Created($"/auth/usuarios/{usuario.Id}", usuario);
});

auth.MapPost("/login", async (LoginRequest req, AuthService svc, CancellationToken ct) =>
    Results.Ok(await svc.LoginAsync(req, ct)));

auth.MapGet("/me", (ClaimsPrincipal user) => Results.Ok(new
{
    id = user.FindFirst("sub")?.Value,
    nome = user.FindFirst("name")?.Value,
    email = user.FindFirst("email")?.Value
})).RequireAuthorization();

app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false });
app.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = c => c.Tags.Contains("ready") });

app.Run();

public partial class Program { }
