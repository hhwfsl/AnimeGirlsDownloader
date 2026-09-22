namespace AnimeGirlsDownloader.Responses;

public sealed class UserProfileResponse
{
    public long Id { get; set; }
    public required string Name { get; set; }
    public string? AvatarUrl { get; set; }
}
