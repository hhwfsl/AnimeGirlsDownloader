using AnimeGirlsDownloader.Models;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace AnimeGirlsDownloader.Requests
{
    public class UploadImageRequest
    {
        public string Creator { get; set; } = "Unknown";
        public List<Tag>? Tags { get; set; } = null;
        public string? Uploader { get; set; }
        public required byte[] Data { get; set; }
        public bool IsNSFW { get; set; }
        public bool IsAiGenerate { get; set; }
    }
    [JsonSourceGenerationOptions(WriteIndented = true)]
    [JsonSerializable(typeof(UploadImageRequest))]
    internal partial class UploadImageRequestContext : JsonSerializerContext
    {

    }
}
