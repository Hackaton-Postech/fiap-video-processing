using System.Text.Json;
using RabbitMQ.Client;

namespace FiapVideo.Worker;

public class EventPublisher(IChannel canal, string exchange)
{
    private readonly SemaphoreSlim _trava = new(1, 1);

    public async Task PublicarAsync<T>(string routingKey, T evento, CancellationToken ct)
    {
        var corpo = JsonSerializer.SerializeToUtf8Bytes(evento, JsonConfig.Options);
        var props = new BasicProperties
        {
            Persistent = true,
            ContentType = "application/json",
            MessageId = Guid.NewGuid().ToString()
        };

        await _trava.WaitAsync(ct);
        try
        {
            await canal.BasicPublishAsync(exchange, routingKey, false, props, corpo, ct);
        }
        finally
        {
            _trava.Release();
        }
    }
}
