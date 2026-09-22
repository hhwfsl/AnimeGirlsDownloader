using AnimeGirlsDownloader.Models;
using System.Collections.Generic;

namespace AnimeGirlsDownloader.Responses;

public sealed class TagResponse
{
    public List<Tag> Tags { get; set; } = [];
}
