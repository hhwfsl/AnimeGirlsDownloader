using Microsoft.UI.Xaml;
using System.Threading.Tasks;
using Windows.Storage;

namespace AnimeGirlsDownloader.Interfaces
{
    public interface IFileService
    {
        public void Initialize(Window window);
        public Task<StorageFolder?> PickFolderAsync();
        public Task<StorageFile?> PickImageAsync();
        public Task<byte[]> ImageToBytes(string filePath);
    }
}
