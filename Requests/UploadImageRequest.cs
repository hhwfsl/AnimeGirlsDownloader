using AnimeGirlsDownloader.Models;
using System.Collections.Generic;

namespace AnimeGirlsDownloader.Requests;

public sealed class UploadImageRequest
{
    public string Creator { get; set; } = "Unknown";
    public List<Tag>? Tags { get; set; }
    public required byte[] Data { get; set; }
    public bool IsNSFW { get; set; }
    public bool IsAiGenerate { get; set; }
}
