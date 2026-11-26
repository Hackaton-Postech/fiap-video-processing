using System.Text.Json;
using FiapVideo.Videos.Application;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace FiapVideo.Videos.Infrastructure;

public class VideoStatusConsumer(
    IServiceScopeFactory scopeFactory,
    RabbitMqOptions options,
    ILogger<VideoStatusConsumer> logger) : BackgroundService
{
    private const ushort Simultaneos = 4;

    private IConnection? _conexao;
    private IChannel? _canal;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await IniciarAsync(stoppingToken);
                break;
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Não foi possível conectar ao RabbitMQ para consumir status. Nova tentativa em 5s.");
                Descartar();
                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    return;
                }
            }
        }

        // Depois de iniciar, a reconexão fica por conta da recuperação automática do cliente.
        try
        {
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        catch (OperationCanceledException)
        {
        }
    }

    private async Task IniciarAsync(CancellationToken ct)
    {
        var fabrica = new ConnectionFactory
        {
            HostName = options.Host,
            Port = options.Port,
            UserName = options.User,
            Password = options.Password,
            ConsumerDispatchConcurrency = Simultaneos
        };

        _conexao = await fabrica.CreateConnectionAsync(ct);
        _canal = await _conexao.CreateChannelAsync(cancellationToken: ct);
        await _canal.ExchangeDeclareAsync(options.Exchange, ExchangeType.Topic, durable: true, cancellationToken: ct);
        await RabbitTopology.DeclararFilasAsync(_canal, options.Exchange, ct);
        await _canal.BasicQosAsync(0, Simultaneos, false, ct);

        var consumidor = new AsyncEventingBasicConsumer(_canal);
        consumidor.ReceivedAsync += async (_, ea) =>
        {
            try
            {
                await TratarAsync(ea, ct);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Erro inesperado ao tratar evento de status.");
            }
        };

        await _canal.BasicConsumeAsync(RabbitTopology.FilaStatus, false, consumidor, ct);
        logger.LogInformation("Consumindo a fila {Fila}.", RabbitTopology.FilaStatus);
    }

    private async Task TratarAsync(BasicDeliverEventArgs ea, CancellationToken ct)
    {
        var canal = _canal!;
        var tag = ea.DeliveryTag;
        var chave = ea.RoutingKey;
        var corpo = ea.Body.ToArray();

        try
        {
            await AplicarAsync(chave, corpo, ct);
            await canal.BasicAckAsync(tag, false, ct);
        }
        catch (JsonException ex)
        {
            logger.LogError(ex, "Evento {Chave} ilegível enviado para a DLQ.", chave);
            await canal.BasicNackAsync(tag, false, false, ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            await canal.BasicNackAsync(tag, false, true, CancellationToken.None);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Falha ao aplicar o evento {Chave}. Será reenfileirado.", chave);
            await Task.Delay(TimeSpan.FromSeconds(2), ct);
            await canal.BasicNackAsync(tag, false, true, ct);
        }
    }

    private async Task AplicarAsync(string chave, byte[] corpo, CancellationToken ct)
    {
        using var escopo = scopeFactory.CreateScope();
        var repositorio = escopo.ServiceProvider.GetRequiredService<IVideoRepository>();

        switch (chave)
        {
            case RoutingKeys.VideoProcessando:
            {
                var e = Ler<VideoProcessando>(corpo);
                var mudou = await repositorio.MarcarProcessandoAsync(e.VideoId, ct);
                logger.LogInformation("Vídeo {VideoId} em processamento (alterado: {Mudou}).", e.VideoId, mudou);
                break;
            }
            case RoutingKeys.VideoProcessado:
            {
                var e = Ler<VideoProcessado>(corpo);
                var mudou = await repositorio.MarcarConcluidoAsync(e.VideoId, e.ChaveZip, ct);
                logger.LogInformation("Vídeo {VideoId} concluído (alterado: {Mudou}).", e.VideoId, mudou);
                break;
            }
            case RoutingKeys.VideoFalhou:
            {
                var e = Ler<VideoFalhou>(corpo);
                var mudou = await repositorio.MarcarFalhaAsync(e.VideoId, e.Mensagem, ct);
                logger.LogInformation("Vídeo {VideoId} com erro (alterado: {Mudou}).", e.VideoId, mudou);
                break;
            }
            default:
                logger.LogWarning("Evento com routing key desconhecida: {Chave}.", chave);
                break;
        }
    }

    private static T Ler<T>(byte[] corpo)
    {
        var evento = JsonSerializer.Deserialize<T>(corpo, OutboxJson.Options)
                     ?? throw new JsonException("Corpo vazio.");
        return evento;
    }

    private void Descartar()
    {
        try { _canal?.Dispose(); } catch { /* conexão já caiu */ }
        try { _conexao?.Dispose(); } catch { /* conexão já caiu */ }
        _canal = null;
        _conexao = null;
    }

    public override void Dispose()
    {
        Descartar();
        base.Dispose();
    }
}
