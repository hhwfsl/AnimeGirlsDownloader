using AnimeGirlsDownloader.Enums;
using AnimeGirlsDownloader.Interfaces;
using AnimeGirlsDownloader.Models;
using AnimeGirlsDownloader.Serialization;
using Microsoft.UI.Xaml;
using System;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace AnimeGirlsDownloader.Services;

public sealed class SettingService : ISettingService
{
    private static readonly string[] SupportedLanguages = ["en-US", "zh-Hans-CN", "ja-JP"];
    private readonly object _syncRoot = new();
    private Settings _settings;
    private long? _activeUserId;
    private Action? _resizeWindowMethod;

    public SettingService()
    {
        AppPaths.EnsureDataDirectories();
        MigrateLegacySettings();
        MigratePreviousUserDirectories();
        _settings = LoadSettingCore(AppPaths.SettingsFilePath, null);
    }

    public void Initialize(Action resizeWindowMethod) =>
        _resizeWindowMethod = resizeWindowMethod ?? throw new ArgumentNullException(nameof(resizeWindowMethod));

    public Settings GetSettings()
    {
        lock (_syncRoot)
        {
            return _settings;
        }
    }

    public Settings LoadSetting()
    {
        lock (_syncRoot)
        {
            _settings = LoadSettingCore(GetCurrentSettingsPath(), _activeUserId);
            return _settings;
        }
    }

    public Settings ActivateUser(long userId, string userName)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(userId);
        ArgumentException.ThrowIfNullOrWhiteSpace(userName);
        lock (_syncRoot)
        {
            SaveSettingCore(_settings, GetCurrentSettingsPath(), _activeUserId);
            string applicationLanguage = LoadSettingCore(AppPaths.SettingsFilePath, null).Language;
            _activeUserId = userId;
            AppPaths.EnsureUserDirectory(userId);
            _settings = LoadSettingCore(AppPaths.GetUserSettingsFilePath(userId), userId);
            _settings.Language = applicationLanguage;
            _settings.LoggedUserId = userId;
            _settings.LoggedUserName = userName.Trim();
            _settings.UserName = userName.Trim();
            SaveSettingCore(_settings, GetCurrentSettingsPath(), _activeUserId);
            return _settings;
        }
    }

    public Settings DeactivateUser()
    {
        lock (_syncRoot)
        {
            SaveSettingCore(_settings, GetCurrentSettingsPath(), _activeUserId);
            _activeUserId = null;
            _settings = LoadSettingCore(AppPaths.SettingsFilePath, null);
            _settings.LoggedUserId = null;
            _settings.LoggedUserName = null;
            SaveSettingCore(_settings, AppPaths.SettingsFilePath, null);
            return _settings;
        }
    }

    public void SaveSetting(Settings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        lock (_syncRoot)
        {
            _settings = settings;
            SaveSettingCore(_settings, GetCurrentSettingsPath(), _activeUserId);
        }
    }

    public void SaveSetting()
    {
        lock (_syncRoot)
        {
            SaveSettingCore(_settings, GetCurrentSettingsPath(), _activeUserId);
        }
    }

    public ISettingService SetWindowWidth(int width) { _settings.WindowWidth = Math.Max(0, width); return this; }
    public ISettingService SetWindowHeight(int height) { _settings.WindowHeight = Math.Max(0, height); return this; }
    public ISettingService SetAppTheme(ElementTheme elementTheme) { _settings.AppTheme = elementTheme; return this; }
    public ISettingService SetIsEnableNSFW(ImageType type) { _settings.ImageType = type; return this; }
    public ISettingService SetIsEnableFixedSavingPath(bool value) { _settings.IsEnableFixedSavingPath = value; return this; }
    public ISettingService SetSavingPath(string? savingPath) { _settings.SavingPath = savingPath; return this; }
    public ISettingService SetLanguage(string language)
    {
        string normalizedLanguage = SupportedLanguages.Contains(language, StringComparer.OrdinalIgnoreCase)
            ? language
            : "en-US";
        _settings.Language = normalizedLanguage;

        // Language is an application-startup setting. Keep it in the anonymous/global
        // configuration so it can be read before token login activates a user profile.
        Settings applicationSettings = _activeUserId is null
            ? _settings
            : LoadSettingCore(AppPaths.SettingsFilePath, null);
        applicationSettings.Language = normalizedLanguage;
        SaveSettingCore(applicationSettings, AppPaths.SettingsFilePath, null);
        return this;
    }
    public ISettingService SetUserName(string userName) { _settings.UserName = userName.Trim(); return this; }
    public ISettingService SetUserAvatarPath(string avatarPath) { _settings.UserAvatarPath = avatarPath; return this; }
    public ISettingService SetLoggedUserName(string? loggedUserName) { _settings.LoggedUserName = loggedUserName; return this; }
    public void ResizeWindowToStandardSize() => _resizeWindowMethod?.Invoke();

    public string SetDefaultUserNameWithSaving()
    {
        _settings.UserName = "User";
        SaveSetting();
        return _settings.UserName;
    }

    private string GetCurrentSettingsPath() => _activeUserId is long userId
        ? AppPaths.GetUserSettingsFilePath(userId)
        : AppPaths.SettingsFilePath;

    private static Settings LoadSettingCore(string path, long? userId)
    {
        if (!File.Exists(path))
        {
            Settings defaults = CreateDefaults(userId);
            SaveSettingCore(defaults, path, userId);
            return defaults;
        }

        try
        {
            Settings settings = JsonSerializer.Deserialize(
                File.ReadAllText(path),
                AppJsonSerializerContext.Default.Settings) ?? CreateDefaults(userId);
            Normalize(settings, userId);
            return settings;
        }
        catch (JsonException exception)
        {
            AppLogger.LogWarning($"Invalid settings file; defaults were restored. {exception.Message}");
            Settings defaults = CreateDefaults(userId);
            SaveSettingCore(defaults, path, userId);
            return defaults;
        }
        catch (IOException exception)
        {
            AppLogger.LogError($"Unable to read settings: {exception.Message}");
            return CreateDefaults(userId);
        }
    }

    private static void SaveSettingCore(Settings settings, string path, long? userId)
    {
        string? directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory)) Directory.CreateDirectory(directory);
        Normalize(settings, userId);
        string json = JsonSerializer.Serialize(settings, AppJsonSerializerContext.Default.Settings);
        string temporaryPath = path + ".tmp";
        File.WriteAllText(temporaryPath, json);
        File.Move(temporaryPath, path, overwrite: true);
    }

    private static Settings CreateDefaults(long? userId)
    {
        var settings = new Settings { LoggedUserId = userId };
        if (userId is long id && File.Exists(AppPaths.GetUserAvatarFilePath(id)))
            settings.UserAvatarPath = AppPaths.GetUserAvatarFilePath(id);
        return settings;
    }

    private static void Normalize(Settings settings, long? userId)
    {
        settings.WindowWidth = Math.Max(0, settings.WindowWidth);
        settings.WindowHeight = Math.Max(0, settings.WindowHeight);
        settings.Language = SupportedLanguages.Contains(settings.Language, StringComparer.OrdinalIgnoreCase)
            ? settings.Language : "en-US";
        settings.UserName = string.IsNullOrWhiteSpace(settings.UserName) ? "User" : settings.UserName.Trim();
        settings.LoggedUserId = userId;
        if (string.IsNullOrWhiteSpace(settings.UserAvatarPath))
            settings.UserAvatarPath = userId is long id && File.Exists(AppPaths.GetUserAvatarFilePath(id))
                ? AppPaths.GetUserAvatarFilePath(id)
                : Path.Combine(AppPaths.AssetsDirectory, "avatar.png");
    }

    private static void MigrateLegacySettings()
    {
        if (File.Exists(AppPaths.SettingsFilePath)) return;
        string? source = new[] { AppPaths.LegacySettingsFilePath, AppPaths.PreviousSettingsFilePath }
            .FirstOrDefault(File.Exists);
        if (source is null) return;
        try
        {
            File.Copy(source, AppPaths.SettingsFilePath, overwrite: false);
            if (string.Equals(source, AppPaths.LegacySettingsFilePath, StringComparison.OrdinalIgnoreCase))
                File.Delete(source);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        { AppLogger.LogWarning($"Legacy settings could not be migrated: {exception.Message}"); }
    }

    private static void MigratePreviousUserDirectories()
    {
        string previousUsersDirectory = Path.Combine(AppPaths.PreviousDataDirectory, "users");
        if (!Directory.Exists(previousUsersDirectory)) return;
        try
        {
            foreach (string sourceDirectory in Directory.EnumerateDirectories(previousUsersDirectory))
            {
                if (!long.TryParse(Path.GetFileName(sourceDirectory), out long userId) || userId <= 0) continue;
                AppPaths.EnsureUserDirectory(userId);
                foreach (string fileName in new[] { "config.json", "avatar.png", "auth.token" })
                {
                    string sourcePath = Path.Combine(sourceDirectory, fileName);
                    string destinationPath = Path.Combine(AppPaths.GetUserDirectory(userId), fileName);
                    if (File.Exists(sourcePath) && !File.Exists(destinationPath))
                        File.Copy(sourcePath, destinationPath, overwrite: false);
                }
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            AppLogger.LogWarning($"Previous user data could not be migrated: {exception.Message}");
        }
    }
}
