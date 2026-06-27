using System.Text.Json.Serialization;

namespace AnimeGirlsDownloader.Responses
{
    public class RegisterResponse
    {
        public required string Token { get; set; }
    }
    [JsonSourceGenerationOptions(WriteIndented = true)]
    [JsonSerializable(typeof(RegisterResponse))]
    internal partial class RegisterResponseContext : JsonSerializerContext
    {

    }
}
