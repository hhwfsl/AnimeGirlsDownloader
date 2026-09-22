using AnimeGirlsDownloader.Enums;
using Microsoft.UI.Xaml;
using System.IO;

namespace AnimeGirlsDownloader.Models;

public sealed class Settings
{
    public int WindowWidth { get; set; }
    public int WindowHeight { get; set; }
    public ElementTheme AppTheme { get; set; } = ElementTheme.Default;

    public ImageType ImageType { get; set; } = ImageType.SFW;
    public ImageIsAllowAiType IsAllowAiGenerated { get; set; } = ImageIsAllowAiType.NotAllowAi;
    public bool IsEnableFixedSavingPath { get; set; }
    public string? SavingPath { get; set; }
    public string Language { get; set; } = "en-US";

    public string UserName { get; set; } = "User";
    public string UserAvatarPath { get; set; } = Path.Combine(AppPaths.AssetsDirectory, "avatar.png");
    public long? LoggedUserId { get; set; }
    public string? LoggedUserName { get; set; }
}
