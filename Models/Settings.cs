using AnimeGirlsDownloader.Enums;
using Microsoft.UI.Xaml;
using System.IO;
using System.Text.Json.Serialization;

namespace AnimeGirlsDownloader.Models
{
    public class Settings
    {
        // Window settings
        public int WindowWidth { get; set; } = 0;
        public int WindowHeight { get; set; } = 0;
        public ElementTheme AppTheme { get; set; } = App.Current.RequestedTheme == ApplicationTheme.Dark ? ElementTheme.Dark : ElementTheme.Light;

        // Browse and download settings
        public ImageType ImageType { get; set; } = ImageType.SFW;
        public bool IsEnableFixedSavingPath { get; set; } = false;
        public string? SavingPath { get; set; } = null;
        public string Language { get; set; } = "en-US";// Only set full language tag here, e.g., en-US, zh-Hans-CN, ja-JP

        // User settings
        public string UserName { get; set; } = "User";
        public string UserAvatarPath { get; set; } = Path.Combine(AppConsts.AppAssetsDirectory, "avatar.png");

        // Logged user settings
        public string? LoggedUserName { get; set; }
        public string? LoggedUserPassword { get; set; }
    }

    [JsonSourceGenerationOptions(WriteIndented = true)]
    [JsonSerializable(typeof(Settings))]
    internal partial class SettingsContext : JsonSerializerContext
    {

    }
}
