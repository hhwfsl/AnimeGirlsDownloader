using System.Threading;
using System.Threading.Tasks;

namespace AnimeGirlsDownloader.Interfaces;

public interface ITokenStore
{
    Task<string?> ReadAsync(CancellationToken cancellationToken = default);
    Task WriteAsync(string token, CancellationToken cancellationToken = default);
    Task ActivateUserAsync(long userId, CancellationToken cancellationToken = default);
    void Delete();
}
