namespace AnimeGirlsDownloader.Models;

public sealed class UploadSourceItem
{
    public string Path { get; set; } = string.Empty;
    public bool IsFolder { get; set; }
    public string IconGlyph => IsFolder ? "\uE8B7" : "\uEB9F";
}
