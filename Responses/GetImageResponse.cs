using AnimeGirlsDownloader.Models;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace AnimeGirlsDownloader.Responses
{
    public class GetImageResponse
    {
        public required byte[] Data { get; set; }
        public required string Id { get; set; }
        public required List<Tag>? Tags { get; set; }
        public bool IsAiGenerate { get; set; }
    }
    [JsonSourceGenerationOptions(WriteIndented = true)]
    [JsonSerializable(typeof(GetImageResponse))]
    internal partial class GetImageResponseContext : JsonSerializerContext
    {

    }
}
