using System.Text.Json.Serialization;

namespace AnimeGirlsDownloader.Requests
{
    public class RegisterRequest
    {
        public required string UserName { get; set; }
        public required string Password { get; set; }
    }
    [JsonSourceGenerationOptions(WriteIndented = true)]
    [JsonSerializable(typeof(RegisterRequest))]
    internal partial class RegisterRequestContext : JsonSerializerContext
    {

    }
}
