using AnimeGirlsDownloader.Enums;
using AnimeGirlsDownloader.Models;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace AnimeGirlsDownloader.Requests
{
    public class GetImageRequest
    {
        public long? Id { get; set; }
        public List<Tag>? Tags { get; set; }
        public ImageType Type { get; set; }
        public bool IsAiGenerate { get; set; }
    }
    [JsonSourceGenerationOptions(WriteIndented = true)]
    [JsonSerializable(typeof(GetImageRequest))]
    internal partial class GetImageRequestContext : JsonSerializerContext
    {

    }
}
