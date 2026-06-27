using AnimeGirlsDownloader.Interfaces;
using Microsoft.UI.Xaml;
using SkiaSharp;
using System;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.Storage.Pickers;

namespace AnimeGirlsDownloader.Services
{
    public class FileService : IFileService
    {
        private Window? _window;
        private IntPtr _hWnd;
        private readonly FolderPicker _folderPicker = new FolderPicker();
        private readonly FileOpenPicker _filePicker = new FileOpenPicker();
        public void Initialize(Window window)
        {
            _window = window;
            _hWnd = WinRT.Interop.WindowNative.GetWindowHandle(_window);
            WinRT.Interop.InitializeWithWindow.Initialize(_folderPicker, _hWnd);
            WinRT.Interop.InitializeWithWindow.Initialize(_filePicker, _hWnd);
            _filePicker.FileTypeFilter.Add(".jpg");
            _filePicker.FileTypeFilter.Add(".png");
            _filePicker.FileTypeFilter.Add(".bmp");
            _filePicker.FileTypeFilter.Add(".jpeg");
        }

        public async Task<StorageFolder?> PickFolderAsync()
        {
            StorageFolder? folder = await _folderPicker.PickSingleFolderAsync();
            return folder;
        }

        public async Task<StorageFile?> PickImageAsync()
        {
            StorageFile? imageFile = await _filePicker.PickSingleFileAsync();
            return imageFile;
        }

        public async Task<byte[]> ImageToBytes(string filePath)
        {
            using var bitmap = SKBitmap.Decode(filePath);
            using var image = SKImage.FromBitmap(bitmap);
            using var data = image.Encode(SKEncodedImageFormat.Png, 100);
            return data.ToArray();
        }
    }
}
