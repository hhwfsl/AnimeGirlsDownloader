using System.Text.Json.Serialization;

namespace AnimeGirlsDownloader.Models
{
    public class User
    {
        public long Id { get; set; }
        public required string Name { get; set; }
        public string? AvatarPath { get; set; }
        public string? PasswordHash { get; set; }
    }
    [JsonSourceGenerationOptions(WriteIndented = true)]
    [JsonSerializable(typeof(User))]
    internal partial class UserContext : JsonSerializerContext
    {

    }
}
