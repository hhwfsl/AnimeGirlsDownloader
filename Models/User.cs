namespace AnimeGirlsDownloader.Models;

public sealed class User
{
    public long Id { get; set; }
    public required string Name { get; set; }
    public string? AvatarPath { get; set; }
    public string? PasswordHash { get; set; }
}
