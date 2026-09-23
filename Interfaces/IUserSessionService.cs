using AnimeGirlsDownloader.Responses;
using System.Threading;
using System.Threading.Tasks;
using System;

namespace AnimeGirlsDownloader.Interfaces;

public interface IUserSessionService
{
    UserProfileResponse? CurrentUser { get; }
    event EventHandler<UserProfileResponse?>? UserChanged;
    Task<UserProfileResponse> SynchronizeAsync(CancellationToken cancellationToken = default);
    Task<UserProfileResponse> UpdateUserNameAsync(string name, CancellationToken cancellationToken = default);
    Task<UserProfileResponse> UpdateAvatarAsync(byte[] pngData, CancellationToken cancellationToken = default);
    void SignOut();
}
