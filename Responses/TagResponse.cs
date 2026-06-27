using AnimeGirlsDownloader.Models;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace AnimeGirlsDownloader.Responses
{
    public class TagResponse
    {
        public List<Tag> Tags { get; set; } = new List<Tag>();
    }
    [JsonSourceGenerationOptions(WriteIndented = true)]
    [JsonSerializable(typeof(TagResponse))]
    internal partial class TagResponseContext : JsonSerializerContext
    {

    }
}
