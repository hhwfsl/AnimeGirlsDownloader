using Microsoft.UI.Xaml;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using Windows.Storage;

namespace AnimeGirlsDownloader.Interfaces;

public interface IFileService
{
    void Initialize(Window window);
    Task<StorageFolder?> PickFolderAsync();
    Task<StorageFile?> PickImageAsync();
    Task<IReadOnlyList<StorageFile>> PickImagesAsync();
    Task<byte[]> EncodeImageAsPngAsync(string filePath, CancellationToken cancellationToken = default);
}
