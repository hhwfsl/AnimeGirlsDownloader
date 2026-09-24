using System;
using System.Globalization;
using System.IO;

namespace AnimeGirlsDownloader;

public static class AppPaths
{
    private const string ApplicationDirectoryName = "AnimeGirlsDownloader";

    public static string InstallDirectory { get; } = Path.GetFullPath(AppContext.BaseDirectory);
    public static string DataDirectory { get; } = InstallDirectory;
    public static string AssetsDirectory { get; } = Path.Combine(InstallDirectory, "Assets");
    public static string LogDirectory { get; } = Path.Combine(InstallDirectory, "logs");
    public static string UsersDirectory { get; } = Path.Combine(InstallDirectory, "users");
    public static string AnonymousUserDirectory { get; } = Path.Combine(UsersDirectory, "anonymous");
    public static string UpdateDirectory { get; } = Path.Combine(InstallDirectory, ".update");
    public static string UpdateDownloadsDirectory { get; } = Path.Combine(UpdateDirectory, "downloads");
    public static string UpdateStagingDirectory { get; } = Path.Combine(UpdateDirectory, "staging");
    public static string SettingsFilePath { get; } = Path.Combine(AnonymousUserDirectory, "config.json");
    public static string ActiveUserFilePath { get; } = Path.Combine(UsersDirectory, "active-user.txt");
    public static string LegacyTokenMigrationMarkerFilePath { get; } = Path.Combine(UsersDirectory, ".legacy-token-migrated");

    internal static string PreviousDataDirectory { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        ApplicationDirectoryName);
    internal static string PreviousSettingsFilePath { get; } = Path.Combine(PreviousDataDirectory, "config.json");
    internal static string PreviousTokenFilePath { get; } = Path.Combine(PreviousDataDirectory, "auth.token");
    internal static string LegacySettingsFilePath { get; } = Path.Combine(InstallDirectory, "config.json");
    internal static string LegacyTokenFilePath { get; } = Path.Combine(InstallDirectory, "auth.token");

    public static void EnsureDataDirectories()
    {
        Directory.CreateDirectory(LogDirectory);
        Directory.CreateDirectory(UsersDirectory);
        Directory.CreateDirectory(AnonymousUserDirectory);
        Directory.CreateDirectory(UpdateDownloadsDirectory);
        Directory.CreateDirectory(UpdateStagingDirectory);
    }

    public static string GetUserDirectory(long userId)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(userId);
        return Path.Combine(UsersDirectory, userId.ToString(CultureInfo.InvariantCulture));
    }

    public static string GetUserSettingsFilePath(long userId) => Path.Combine(GetUserDirectory(userId), "config.json");

    public static string GetUserAvatarFilePath(long userId) => Path.Combine(GetUserDirectory(userId), "avatar.png");

    public static string GetUserTokenFilePath(long userId) => Path.Combine(GetUserDirectory(userId), "auth.token");

    public static void EnsureUserDirectory(long userId) => Directory.CreateDirectory(GetUserDirectory(userId));
}
