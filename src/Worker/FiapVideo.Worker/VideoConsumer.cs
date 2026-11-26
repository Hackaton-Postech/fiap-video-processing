using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace FiapVideo.Worker;

public class VideoConsumer(
    VideoProcessor processador,
    RabbitMqOptions rabbit,
    ProcessingOptions processamento,
    ILogger<VideoConsumer> logger) : BackgroundService
{
    private IConnection? _conexao;
    private IChannel? _canalConsumo;
    private IChannel? _canalPublicacao;
    private EventPublisher? _publicador;

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

        // A reconexão depois de iniciar fica por conta da recuperação automática do cliente.
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
        var simultaneos = (ushort)Math.Max(1, processamento.Concurrency);

        var fabrica = new ConnectionFactory
        {
            HostName = rabbit.Host,
            Port = rabbit.Port,
            UserName = rabbit.User,
            Password = rabbit.Password,
            ConsumerDispatchConcurrency = simultaneos
        };

        _conexao = await fabrica.CreateConnectionAsync(ct);
        _canalPublicacao = await _conexao.CreateChannelAsync(
            new CreateChannelOptions(publisherConfirmationsEnabled: true, publisherConfirmationTrackingEnabled: true), ct);
        _publicador = new EventPublisher(_canalPublicacao, rabbit.Exchange);

        _canalConsumo = await _conexao.CreateChannelAsync(cancellationToken: ct);
        await RabbitTopology.DeclararAsync(_canalConsumo, rabbit.Exchange, ct);
        await _canalConsumo.BasicQosAsync(0, simultaneos, false, ct);

        var consumidor = new AsyncEventingBasicConsumer(_canalConsumo);
        consumidor.ReceivedAsync += async (_, ea) =>
        {
            try
            {
                await TratarAsync(ea, ct);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Erro inesperado ao tratar a mensagem.");
            }
        };

        await _canalConsumo.BasicConsumeAsync(RabbitTopology.FilaProcessar, false, consumidor, ct);
        logger.LogInformation("Worker consumindo a fila {Fila} ({N} vídeo(s) simultâneo(s)).",
            RabbitTopology.FilaProcessar, simultaneos);
    }

    private async Task TratarAsync(BasicDeliverEventArgs ea, CancellationToken ct)
    {
        var canal = _canalConsumo!;
        var publicador = _publicador!;
        var tag = ea.DeliveryTag;

        VideoEnviado? msg = null;
        try
        {
            msg = JsonSerializer.Deserialize<VideoEnviado>(ea.Body.Span, JsonConfig.Options);
        }
        catch (JsonException ex)
        {
            logger.LogError(ex, "Mensagem ilegível.");
        }

        if (msg is null || msg.VideoId == Guid.Empty || string.IsNullOrWhiteSpace(msg.ChaveOriginal))
        {
            logger.LogError("Mensagem inválida enviada para a DLQ.");
            await canal.BasicNackAsync(tag, false, false, ct);
            return;
        }

        using var escopo = logger.BeginScope(new Dictionary<string, object> { ["videoId"] = msg.VideoId });
        var tentativa = NumeroDaEntrega(ea);

        try
        {
            await publicador.PublicarAsync(RoutingKeys.VideoProcessando,
                new VideoProcessando(msg.VideoId, DateTime.UtcNow), ct);

            var relogio = Stopwatch.StartNew();
            var resultado = await processador.ProcessarAsync(msg, ct);

            await publicador.PublicarAsync(RoutingKeys.VideoProcessado,
                new VideoProcessado(msg.VideoId, msg.UsuarioId, msg.Email, msg.Nome, msg.NomeOriginal,
                    resultado.ChaveZip, resultado.TotalFrames, DateTime.UtcNow), ct);

            await canal.BasicAckAsync(tag, false, ct);
            logger.LogInformation("Vídeo processado: {Frames} frame(s) em {Ms} ms.",
                resultado.TotalFrames, relogio.ElapsedMilliseconds);
        }
        catch (ProcessamentoException ex)
        {
            logger.LogWarning("Falha definitiva no processamento: {Mensagem}", ex.Message);
            await FalharAsync(msg, ex.Message, tag, ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            await canal.BasicNackAsync(tag, false, true, CancellationToken.None);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Falha técnica (tentativa {Tentativa} de {Max}).", tentativa, processamento.MaxTentativas);
            if (tentativa >= processamento.MaxTentativas)
            {
                await FalharAsync(msg, "Falha técnica no processamento após várias tentativas.", tag, ct);
            }
            else
            {
                await Task.Delay(TimeSpan.FromSeconds(2 * tentativa), ct);
                await canal.BasicNackAsync(tag, false, true, ct);
            }
        }
    }

    private async Task FalharAsync(VideoEnviado msg, string motivo, ulong tag, CancellationToken ct)
    {
        await _publicador!.PublicarAsync(RoutingKeys.VideoFalhou,
            new VideoFalhou(msg.VideoId, msg.UsuarioId, msg.Email, msg.Nome, msg.NomeOriginal, motivo, DateTime.UtcNow), ct);
        await _canalConsumo!.BasicAckAsync(tag, false, ct);
    }

    private static int NumeroDaEntrega(BasicDeliverEventArgs ea)
    {
        if (ea.BasicProperties.Headers is { } cabecalhos &&
            cabecalhos.TryGetValue("x-delivery-count", out var valor) && valor is not null)
        {
            try
            {
                return (int)Convert.ToInt64(valor) + 1;
            }
            catch
            {
                // cabeçalho em formato inesperado: trata como primeira entrega
            }
        }
        return 1;
    }

    private void Descartar()
    {
        try { _canalConsumo?.Dispose(); } catch { /* conexão já caiu */ }
        try { _canalPublicacao?.Dispose(); } catch { /* conexão já caiu */ }
        try { _conexao?.Dispose(); } catch { /* conexão já caiu */ }
        _canalConsumo = null;
        _canalPublicacao = null;
        _conexao = null;
        _publicador = null;
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        await base.StopAsync(cancellationToken);
        Descartar();
    }
}
