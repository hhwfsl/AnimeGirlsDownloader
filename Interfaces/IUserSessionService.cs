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
    void SignOut();
}
