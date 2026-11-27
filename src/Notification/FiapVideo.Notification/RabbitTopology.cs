using RabbitMQ.Client;

namespace FiapVideo.Notification;

// Esta topologia é declarada igual na Video API. Qualquer mudança precisa ser feita nos dois lugares.
public static class RabbitTopology
{
    public const string FilaNotificacao = "video.notificacao";
    public const string ExchangeDeadLetter = "video.dlx";
    public const string FilaNotificacaoDlq = "video.notificacao.dlq";

    public static async Task DeclararAsync(IChannel canal, string exchange, CancellationToken ct)
    {
        await canal.ExchangeDeclareAsync(exchange, ExchangeType.Topic, durable: true, cancellationToken: ct);
        await canal.ExchangeDeclareAsync(ExchangeDeadLetter, ExchangeType.Direct, durable: true, cancellationToken: ct);

        await canal.QueueDeclareAsync(FilaNotificacaoDlq, durable: true, exclusive: false, autoDelete: false,
            arguments: null, cancellationToken: ct);
        await canal.QueueBindAsync(FilaNotificacaoDlq, ExchangeDeadLetter, FilaNotificacao, cancellationToken: ct);

        var argumentos = new Dictionary<string, object?>
        {
            ["x-queue-type"] = "quorum",
            ["x-dead-letter-exchange"] = ExchangeDeadLetter,
            ["x-dead-letter-routing-key"] = FilaNotificacao,
            ["x-delivery-limit"] = 5
        };
        await canal.QueueDeclareAsync(FilaNotificacao, durable: true, exclusive: false, autoDelete: false,
            arguments: argumentos, cancellationToken: ct);
        await canal.QueueBindAsync(FilaNotificacao, exchange, RoutingKeys.VideoFalhou, cancellationToken: ct);
    }
}
