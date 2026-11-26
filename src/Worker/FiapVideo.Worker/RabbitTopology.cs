using RabbitMQ.Client;

namespace FiapVideo.Worker;

// Esta topologia é declarada igual na Video API. Qualquer mudança precisa ser feita nos dois lugares.
public static class RabbitTopology
{
    public const string FilaProcessar = "video.processar";
    public const string ExchangeDeadLetter = "video.dlx";
    public const string FilaProcessarDlq = "video.processar.dlq";

    public static async Task DeclararAsync(IChannel canal, string exchange, CancellationToken ct)
    {
        await canal.ExchangeDeclareAsync(exchange, ExchangeType.Topic, durable: true, cancellationToken: ct);
        await canal.ExchangeDeclareAsync(ExchangeDeadLetter, ExchangeType.Direct, durable: true, cancellationToken: ct);

        await canal.QueueDeclareAsync(FilaProcessarDlq, durable: true, exclusive: false, autoDelete: false,
            arguments: null, cancellationToken: ct);
        await canal.QueueBindAsync(FilaProcessarDlq, ExchangeDeadLetter, FilaProcessar, cancellationToken: ct);

        var argumentos = new Dictionary<string, object?>
        {
            ["x-queue-type"] = "quorum",
            ["x-dead-letter-exchange"] = ExchangeDeadLetter,
            ["x-dead-letter-routing-key"] = FilaProcessar,
            ["x-delivery-limit"] = 5
        };
        await canal.QueueDeclareAsync(FilaProcessar, durable: true, exclusive: false, autoDelete: false,
            arguments: argumentos, cancellationToken: ct);
        await canal.QueueBindAsync(FilaProcessar, exchange, RoutingKeys.VideoEnviado, cancellationToken: ct);
    }
}
