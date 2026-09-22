using AnimeGirlsDownloader.Enums;
using AnimeGirlsDownloader.Models;
using System.Collections.Generic;

namespace AnimeGirlsDownloader.Requests;

public sealed class GetImageRequest
{
    public long? Id { get; set; }
    public List<Tag>? Tags { get; set; }
    public ImageType Type { get; set; }
    public ImageIsAllowAiType IsAllowAiGenerated { get; set; }
}
