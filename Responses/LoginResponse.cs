using System.Text.Json.Serialization;

namespace AnimeGirlsDownloader.Responses
{
    public class LoginResponse
    {
        public required string Token { get; set; }
    }

    [JsonSourceGenerationOptions(WriteIndented = true)]
    [JsonSerializable(typeof(LoginResponse))]
    internal partial class LoginResponseContext : JsonSerializerContext
    {

    }
}
