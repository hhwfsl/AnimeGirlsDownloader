using AnimeGirlsDownloader.Enums;
using AnimeGirlsDownloader.Models;
using AnimeGirlsDownloader.Requests;
using AnimeGirlsDownloader.Responses;
using System.Collections.Generic;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace AnimeGirlsDownloader.Interfaces;

public interface IAnimeGirlsApiClient
{
    Task<bool> LoginAsync(string userName, string password, CancellationToken cancellationToken = default);
    Task<bool> LoginWithStoredTokenAsync(CancellationToken cancellationToken = default);
    Task<bool> RegisterAsync(string userName, string password, CancellationToken cancellationToken = default);
    Task<GetImageResponse> GetImageByIdAsync(long imageId, CancellationToken cancellationToken = default);
    Task<GetImageResponse> GetRandomImageAsync(
        IReadOnlyCollection<Tag>? tags,
        ImageType type,
        ImageIsAllowAiType isAllowAiGenerated,
        CancellationToken cancellationToken = default);
    Task<string> UploadImageAsync(UploadImageRequest request, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Tag>> GetTagsAsync(string partialTag, CancellationToken cancellationToken = default);
    Task<UserProfileResponse> GetCurrentUserAsync(CancellationToken cancellationToken = default);
    Task<byte[]?> DownloadAvatarAsync(string avatarUrl, CancellationToken cancellationToken = default);
    Task<string> DownloadImageAsync(
        string downloadUrl,
        string destinationDirectory,
        string fallbackFileName,
        IProgress<double>? progress = null,
        CancellationToken cancellationToken = default);
}
