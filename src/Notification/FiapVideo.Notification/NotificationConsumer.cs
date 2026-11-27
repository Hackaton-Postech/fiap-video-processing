using System.Text.Json;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MimeKit;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace FiapVideo.Notification;

public class NotificationConsumer(
    INotificador notificador,
    RabbitMqOptions rabbit,
    ILogger<NotificationConsumer> logger) : BackgroundService
{
    private const ushort Simultaneos = 2;

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
                logger.LogWarning(ex, "Não foi possível conectar ao RabbitMQ. Nova tentativa em 5s.");
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
            HostName = rabbit.Host,
            Port = rabbit.Port,
            UserName = rabbit.User,
            Password = rabbit.Password,
            ConsumerDispatchConcurrency = Simultaneos
        };

        _conexao = await fabrica.CreateConnectionAsync(ct);
        _canal = await _conexao.CreateChannelAsync(cancellationToken: ct);
        await RabbitTopology.DeclararAsync(_canal, rabbit.Exchange, ct);
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
                logger.LogError(ex, "Erro inesperado ao tratar a notificação.");
            }
        };

        await _canal.BasicConsumeAsync(RabbitTopology.FilaNotificacao, false, consumidor, ct);
        logger.LogInformation("Consumindo a fila {Fila}.", RabbitTopology.FilaNotificacao);
    }

    private async Task TratarAsync(BasicDeliverEventArgs ea, CancellationToken ct)
    {
        var canal = _canal!;
        var tag = ea.DeliveryTag;

        VideoFalhou? evento = null;
        try
        {
            evento = JsonSerializer.Deserialize<VideoFalhou>(ea.Body.Span, JsonConfig.Options);
        }
        catch (JsonException ex)
        {
            logger.LogError(ex, "Mensagem ilegível enviada para a DLQ.");
        }

        if (evento is null || evento.VideoId == Guid.Empty)
        {
            await canal.BasicNackAsync(tag, false, false, ct);
            return;
        }

        if (string.IsNullOrWhiteSpace(evento.Email) || !MailboxAddress.TryParse(evento.Email, out _))
        {
            logger.LogWarning("Vídeo {VideoId} sem e-mail válido. Nenhuma notificação enviada.", evento.VideoId);
            await canal.BasicAckAsync(tag, false, ct);
            return;
        }

        try
        {
            await notificador.EnviarAsync(NotificacaoFalha.Montar(evento), ct);
            await canal.BasicAckAsync(tag, false, ct);
            logger.LogInformation("Notificação de falha enviada para o vídeo {VideoId}.", evento.VideoId);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            await canal.BasicNackAsync(tag, false, true, CancellationToken.None);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Falha ao enviar o e-mail do vídeo {VideoId}. Será reenfileirado.", evento.VideoId);
            await Task.Delay(TimeSpan.FromSeconds(5), ct);
            await canal.BasicNackAsync(tag, false, true, ct);
        }
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
