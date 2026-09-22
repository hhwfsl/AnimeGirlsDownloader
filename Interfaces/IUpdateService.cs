using AnimeGirlsDownloader.Models;
using System.Threading;
using System.Threading.Tasks;

namespace AnimeGirlsDownloader.Interfaces;

public interface IUpdateService
{
    string CurrentVersion { get; }
    Task<UpdateCheckResult> CheckForUpdateAsync(CancellationToken cancellationToken = default);
}
