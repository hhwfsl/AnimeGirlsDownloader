using AnimeGirlsDownloader.Interfaces;
using AnimeGirlsDownloader.Models;
using AnimeGirlsDownloader.Responses;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SkiaSharp;

namespace AnimeGirlsDownloader.Services;

public sealed class UserSessionService : IUserSessionService
{
    private readonly IAnimeGirlsApiClient _apiClient;
    private readonly ISettingService _settingService;
    private readonly ITokenStore _tokenStore;

    public UserSessionService(
        IAnimeGirlsApiClient apiClient,
        ISettingService settingService,
        ITokenStore tokenStore)
    {
        _apiClient = apiClient;
        _settingService = settingService;
        _tokenStore = tokenStore;
    }

    public UserProfileResponse? CurrentUser { get; private set; }
    public event EventHandler<UserProfileResponse?>? UserChanged;

    public async Task<UserProfileResponse> SynchronizeAsync(CancellationToken cancellationToken = default)
    {
        UserProfileResponse profile = await _apiClient.GetCurrentUserAsync(cancellationToken);
        try
        {
            await _tokenStore.ActivateUserAsync(profile.Id, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            throw new ApiClientException(
                "The account was authenticated, but its local token could not be saved.",
                innerException: exception);
        }

        Settings settings = _settingService.ActivateUser(profile.Id, profile.Name);
        settings.LoggedUserId = profile.Id;
        settings.LoggedUserName = profile.Name;
        settings.UserName = profile.Name;

        if (!string.IsNullOrWhiteSpace(profile.AvatarUrl))
        {
            try
            {
                byte[]? avatarBytes = await _apiClient.DownloadAvatarAsync(profile.AvatarUrl, cancellationToken);
                if (avatarBytes is { Length: > 0 })
                {
                    AppPaths.EnsureUserDirectory(profile.Id);
                    string avatarPath = AppPaths.GetUserAvatarFilePath(profile.Id);
                    string temporaryPath = avatarPath + ".tmp";
                    using SKBitmap bitmap = SKBitmap.Decode(avatarBytes)
                        ?? throw new InvalidDataException("The server returned an invalid avatar image.");
                    using SKImage image = SKImage.FromBitmap(bitmap);
                    using SKData data = image.Encode(SKEncodedImageFormat.Png, 100)
                        ?? throw new InvalidDataException("The avatar image could not be encoded.");
                    await File.WriteAllBytesAsync(temporaryPath, data.ToArray(), cancellationToken);
                    File.Move(temporaryPath, avatarPath, overwrite: true);
                    settings.UserAvatarPath = avatarPath;
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                AppLogger.LogWarning($"Unable to synchronize the user avatar; the cached avatar is retained. {exception.Message}");
            }
        }
        else
        {
            string avatarPath = AppPaths.GetUserAvatarFilePath(profile.Id);
            settings.UserAvatarPath = Path.Combine(AppPaths.AssetsDirectory, "avatar.png");
            try
            {
                if (File.Exists(avatarPath))
                    File.Delete(avatarPath);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                AppLogger.LogWarning($"Unable to remove a stale cached avatar. {exception.Message}");
            }
        }

        _settingService.SaveSetting(settings);
        CurrentUser = profile;
        UserChanged?.Invoke(this, profile);
        return profile;
    }

    public async Task<UserProfileResponse> UpdateUserNameAsync(
        string name,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        await _apiClient.UpdateUserNameAsync(name.Trim(), cancellationToken);
        return await SynchronizeAsync(cancellationToken);
    }

    public async Task<UserProfileResponse> UpdateAvatarAsync(
        byte[] pngData,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(pngData);
        await _apiClient.UpdateAvatarAsync(pngData, cancellationToken);
        return await SynchronizeAsync(cancellationToken);
    }

    public void SignOut()
    {
        _tokenStore.Delete();
        CurrentUser = null;
        _settingService.DeactivateUser();
        UserChanged?.Invoke(this, null);
    }
}
