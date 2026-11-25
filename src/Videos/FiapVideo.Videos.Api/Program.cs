using System.Security.Claims;
using System.Text;
using FiapVideo.Videos.Application;
using FiapVideo.Videos.Domain;
using FiapVideo.Videos.Infrastructure;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("Videos");
if (string.IsNullOrWhiteSpace(connectionString))
    throw new InvalidOperationException("ConnectionStrings:Videos não configurada.");

var jwtSecret = builder.Configuration["Jwt:Secret"] ?? string.Empty;
if (jwtSecret.Length < 32)
    throw new InvalidOperationException("Jwt:Secret deve ter ao menos 32 caracteres.");
var jwtIssuer = builder.Configuration["Jwt:Issuer"] ?? "fiap-video-auth";
var jwtAudience = builder.Configuration["Jwt:Audience"] ?? "fiap-video";

var storage = builder.Configuration.GetSection("Storage").Get<StorageOptions>() ?? new StorageOptions();
var rabbit = builder.Configuration.GetSection("RabbitMq").Get<RabbitMqOptions>() ?? new RabbitMqOptions();

builder.Services.AddVideosInfrastructure(connectionString, storage, rabbit);

const long limiteCorpo = VideoService.TamanhoMaximoBytes + 1024 * 1024;
builder.WebHost.ConfigureKestrel(o => o.Limits.MaxRequestBodySize = limiteCorpo);
builder.Services.Configure<FormOptions>(o => o.MultipartBodyLengthLimit = limiteCorpo);

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(o =>
    {
        o.MapInboundClaims = false;
        o.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwtIssuer,
            ValidateAudience = true,
            ValidAudience = jwtAudience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30)
        };
    });
builder.Services.AddAuthorization();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddHealthChecks()
    .AddDbContextCheck<VideosDbContext>("db", tags: new[] { "ready" });

var app = builder.Build();

if (app.Configuration.GetValue<bool>("Database:MigrateOnStartup"))
{
    using var scope = app.Services.CreateScope();
    scope.ServiceProvider.GetRequiredService<VideosDbContext>().Database.Migrate();
}

app.Use(async (ctx, next) =>
{
    try
    {
        await next();
    }
    catch (Exception ex) when (ex is ValidacaoException or DomainException or NaoEncontradoException or ConflitoException)
    {
        var (status, code) = ex switch
        {
            NaoEncontradoException => (StatusCodes.Status404NotFound, "nao_encontrado"),
            ConflitoException => (StatusCodes.Status409Conflict, "conflito"),
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

static Guid UsuarioId(ClaimsPrincipal u) =>
    Guid.TryParse(u.FindFirst("sub")?.Value, out var id) ? id : Guid.Empty;

var videos = app.MapGroup("/videos").RequireAuthorization();

videos.MapPost("", async (IFormFile? arquivo, ClaimsPrincipal user, VideoService svc, CancellationToken ct) =>
{
    var usuarioId = UsuarioId(user);
    if (usuarioId == Guid.Empty) return Results.Unauthorized();
    if (arquivo is null)
        throw new ValidacaoException("Envie o arquivo no campo 'arquivo' (multipart/form-data).");

    await using var stream = arquivo.OpenReadStream();
    var resposta = await svc.EnviarAsync(new EnviarVideoCommand(
        usuarioId,
        user.FindFirst("email")?.Value ?? string.Empty,
        user.FindFirst("name")?.Value ?? string.Empty,
        arquivo.FileName,
        arquivo.ContentType,
        arquivo.Length,
        stream), ct);

    return Results.Accepted($"/videos/{resposta.Id}", resposta);
}).DisableAntiforgery();

videos.MapGet("", async (ClaimsPrincipal user, VideoService svc, CancellationToken ct) =>
{
    var usuarioId = UsuarioId(user);
    if (usuarioId == Guid.Empty) return Results.Unauthorized();
    return Results.Ok(await svc.ListarAsync(usuarioId, ct));
});

videos.MapGet("/{id:guid}", async (Guid id, ClaimsPrincipal user, VideoService svc, CancellationToken ct) =>
{
    var usuarioId = UsuarioId(user);
    if (usuarioId == Guid.Empty) return Results.Unauthorized();
    return Results.Ok(await svc.ObterAsync(usuarioId, id, ct));
});

videos.MapGet("/{id:guid}/download", async (Guid id, ClaimsPrincipal user, VideoService svc, CancellationToken ct) =>
{
    var usuarioId = UsuarioId(user);
    if (usuarioId == Guid.Empty) return Results.Unauthorized();
    var zip = await svc.BaixarZipAsync(usuarioId, id, ct);
    return Results.File(zip.Conteudo, "application/zip", zip.NomeArquivo);
});

app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false });
app.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = c => c.Tags.Contains("ready") });

app.Run();

public partial class Program { }
