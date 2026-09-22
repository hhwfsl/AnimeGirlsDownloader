namespace AnimeGirlsDownloader.Models;

public sealed class Tag
{
    public long Id { get; set; }
    public required string Name { get; set; } = string.Empty;

    public override string ToString() => Name;
}
