using System.Text.Json.Serialization;

namespace AnimeGirlsDownloader.Responses
{
    public class MessageResponse
    {
        public required string Message { get; set; }
    }

    [JsonSourceGenerationOptions(WriteIndented = true)]
    [JsonSerializable(typeof(MessageResponse))]
    internal partial class MessageResponseContext : JsonSerializerContext
    {

    }
}
