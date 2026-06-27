

using System.Text.Json.Serialization;

namespace AnimeGirlsDownloader.Requests
{
    public class TagRequest
    {
        public string PartialTag { get; set; } = string.Empty;
    }
    [JsonSourceGenerationOptions(WriteIndented = true)]
    [JsonSerializable(typeof(TagRequest))]
    internal partial class TagRequestContext : JsonSerializerContext
    {

    }
}
