using System.Text.Json.Serialization;

namespace AnimeGirlsDownloader.Models
{
    public class Tag
    {
        public long Id { get; set; }
        public required string Name { get; set; } = string.Empty;

        public override string ToString()
        {
            return Name;
        }
    }
    [JsonSourceGenerationOptions(WriteIndented = true)]
    [JsonSerializable(typeof(Tag))]
    internal partial class TagContext : JsonSerializerContext
    {

    }
}
