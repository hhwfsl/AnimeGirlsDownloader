using System.Threading;
using System.Threading.Tasks;

namespace AnimeGirlsDownloader.Interfaces;

public interface ISelfUpdateService
{
    Task PrepareAndLaunchAsync(
        string packagePath,
        string version,
        CancellationToken cancellationToken = default);
}
