
using System.Text.Json.Serialization;

namespace AnimeGirlsDownloader.Requests
{
    public class LoginRequest
    {
        public required string UserName { get; set; }
        public required string Password { get; set; }
    }
    [JsonSourceGenerationOptions(WriteIndented = true)]
    [JsonSerializable(typeof(LoginRequest))]
    internal partial class LoginRequestContext : JsonSerializerContext
    {

    }
}
