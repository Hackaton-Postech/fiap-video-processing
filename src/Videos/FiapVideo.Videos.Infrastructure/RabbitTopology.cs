using RabbitMQ.Client;

namespace FiapVideo.Videos.Infrastructure;

// Esta topologia é declarada igual no Worker (filas de processamento). Mudanças precisam ser feitas nos dois lugares.
public static class RabbitTopology
{
    public const string FilaProcessar = "video.processar";
    public const string ExchangeDeadLetter = "video.dlx";
    public const string FilaProcessarDlq = "video.processar.dlq";
    public const string FilaStatus = "video.status";
    public const string FilaStatusDlq = "video.status.dlq";

    public static async Task DeclararFilasAsync(IChannel canal, string exchange, CancellationToken ct)
    {
        await canal.ExchangeDeclareAsync(ExchangeDeadLetter, ExchangeType.Direct, durable: true, cancellationToken: ct);

        // Fila de trabalho do Worker (video.enviado).
        await DeclararComDlqAsync(canal, FilaProcessar, FilaProcessarDlq, ct);
        await canal.QueueBindAsync(FilaProcessar, exchange, RoutingKeys.VideoEnviado, cancellationToken: ct);

        // Fila de status desta API (eventos publicados pelo Worker).
        await DeclararComDlqAsync(canal, FilaStatus, FilaStatusDlq, ct);
        await canal.QueueBindAsync(FilaStatus, exchange, RoutingKeys.VideoProcessando, cancellationToken: ct);
        await canal.QueueBindAsync(FilaStatus, exchange, RoutingKeys.VideoProcessado, cancellationToken: ct);
        await canal.QueueBindAsync(FilaStatus, exchange, RoutingKeys.VideoFalhou, cancellationToken: ct);
    }

    private static async Task DeclararComDlqAsync(IChannel canal, string fila, string dlq, CancellationToken ct)
    {
        await canal.QueueDeclareAsync(dlq, durable: true, exclusive: false, autoDelete: false,
            arguments: null, cancellationToken: ct);
        await canal.QueueBindAsync(dlq, ExchangeDeadLetter, fila, cancellationToken: ct);

        var argumentos = new Dictionary<string, object?>
        {
            ["x-queue-type"] = "quorum",
            ["x-dead-letter-exchange"] = ExchangeDeadLetter,
            ["x-dead-letter-routing-key"] = fila,
            ["x-delivery-limit"] = 5
        };
        await canal.QueueDeclareAsync(fila, durable: true, exclusive: false, autoDelete: false,
            arguments: argumentos, cancellationToken: ct);
    }
}
