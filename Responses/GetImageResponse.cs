using AnimeGirlsDownloader.Models;
using System.Collections.Generic;

namespace AnimeGirlsDownloader.Responses;

public sealed class GetImageResponse
{
    public required string Id { get; set; }
    public required string PreviewUrl { get; set; }
    public required string DownloadUrl { get; set; }
    public List<Tag>? Tags { get; set; }
    public bool IsAiGenerate { get; set; }
}
