using AnimeGirlsDownloader.Interfaces;
using Microsoft.UI.Xaml;
using SkiaSharp;
using System;
using System.IO;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.Storage.Pickers;

namespace AnimeGirlsDownloader.Services;

public sealed class FileService : IFileService
{
    private readonly FolderPicker _folderPicker = new();
    private readonly FileOpenPicker _filePicker = new();
    private bool _isInitialized;

    public void Initialize(Window window)
    {
        ArgumentNullException.ThrowIfNull(window);
        if (_isInitialized)
        {
            return;
        }

        IntPtr windowHandle = WinRT.Interop.WindowNative.GetWindowHandle(window);
        WinRT.Interop.InitializeWithWindow.Initialize(_folderPicker, windowHandle);
        WinRT.Interop.InitializeWithWindow.Initialize(_filePicker, windowHandle);

        foreach (string extension in new[] { ".jpg", ".jpeg", ".png", ".bmp", ".webp" })
        {
            _filePicker.FileTypeFilter.Add(extension);
        }

        _isInitialized = true;
    }

    public Task<StorageFolder?> PickFolderAsync()
    {
        EnsureInitialized();
        return _folderPicker.PickSingleFolderAsync().AsTask();
    }

    public Task<StorageFile?> PickImageAsync()
    {
        EnsureInitialized();
        return _filePicker.PickSingleFileAsync().AsTask();
    }

    public async Task<IReadOnlyList<StorageFile>> PickImagesAsync()
    {
        EnsureInitialized();
        return await _filePicker.PickMultipleFilesAsync();
    }

    public Task<byte[]> EncodeImageAsPngAsync(string filePath, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        return Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            using SKBitmap bitmap = SKBitmap.Decode(filePath)
                ?? throw new InvalidDataException("The selected file is not a supported image.");
            using SKImage image = SKImage.FromBitmap(bitmap);
            using SKData data = image.Encode(SKEncodedImageFormat.Png, 100)
                ?? throw new InvalidDataException("The selected image could not be encoded.");
            return data.ToArray();
        }, cancellationToken);
    }

    private void EnsureInitialized()
    {
        if (!_isInitialized)
        {
            throw new InvalidOperationException("The file picker has not been initialized with a window.");
        }
    }
}
