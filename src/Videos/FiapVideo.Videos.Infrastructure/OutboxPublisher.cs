using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;

namespace FiapVideo.Videos.Infrastructure;

public class OutboxPublisher(
    IServiceScopeFactory scopeFactory,
    RabbitMqOptions options,
    ILogger<OutboxPublisher> logger) : BackgroundService
{
    private const int MaxTentativas = 10;
    private const int TamanhoLote = 50;
    private static readonly TimeSpan Intervalo = TimeSpan.FromSeconds(2);

    private IConnection? _connection;
    private IChannel? _channel;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessarLoteAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Falha ao processar o outbox. Nova tentativa em instantes.");
                DescartarCanal();
            }

            try
            {
                await Task.Delay(Intervalo, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private async Task ProcessarLoteAsync(CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<VideosDbContext>();

        var pendentes = await db.OutboxMessages
            .Where(m => m.PublicadoEm == null && m.Tentativas < MaxTentativas)
            .OrderBy(m => m.CriadoEm)
            .Take(TamanhoLote)
            .ToListAsync(ct);

        if (pendentes.Count == 0) return;

        var canal = await ObterCanalAsync(ct);

        foreach (var mensagem in pendentes)
        {
            try
            {
                var props = new BasicProperties
                {
                    Persistent = true,
                    ContentType = "application/json",
                    MessageId = mensagem.Id.ToString()
                };

                await canal.BasicPublishAsync(
                    options.Exchange, mensagem.Tipo, false, props,
                    Encoding.UTF8.GetBytes(mensagem.Payload), ct);

                mensagem.PublicadoEm = DateTime.UtcNow;
                await db.SaveChangesAsync(ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                mensagem.Tentativas++;
                mensagem.UltimoErro = Truncar(ex.Message);
                await db.SaveChangesAsync(ct);

                logger.LogWarning(ex, "Falha ao publicar a mensagem {MessageId} (tentativa {Tentativa} de {Max}).",
                    mensagem.Id, mensagem.Tentativas, MaxTentativas);

                DescartarCanal();
                break;
            }
        }
    }

    private async Task<IChannel> ObterCanalAsync(CancellationToken ct)
    {
        if (_channel is { IsOpen: true }) return _channel;

        DescartarCanal();

        var factory = new ConnectionFactory
        {
            HostName = options.Host,
            Port = options.Port,
            UserName = options.User,
            Password = options.Password
        };

        _connection = await factory.CreateConnectionAsync(ct);
        _channel = await _connection.CreateChannelAsync(
            new CreateChannelOptions(publisherConfirmationsEnabled: true, publisherConfirmationTrackingEnabled: true),
            ct);

        await _channel.ExchangeDeclareAsync(options.Exchange, ExchangeType.Topic, durable: true, cancellationToken: ct);
        await RabbitTopology.DeclararFilasAsync(_channel, options.Exchange, ct);
        return _channel;
    }

    private void DescartarCanal()
    {
        try { _channel?.Dispose(); } catch { /* conexão já caiu */ }
        try { _connection?.Dispose(); } catch { /* conexão já caiu */ }
        _channel = null;
        _connection = null;
    }

    private static string Truncar(string texto) => texto.Length > 1000 ? texto[..1000] : texto;

    public override void Dispose()
    {
        DescartarCanal();
        base.Dispose();
    }
}
