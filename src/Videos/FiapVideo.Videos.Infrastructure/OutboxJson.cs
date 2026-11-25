using System.Text.Json;

namespace FiapVideo.Videos.Infrastructure;

public static class OutboxJson
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);
}
