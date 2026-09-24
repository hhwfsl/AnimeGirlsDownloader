using AnimeGirlsDownloader.Enums;
using AnimeGirlsDownloader.Interfaces;
using AnimeGirlsDownloader.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;

namespace AnimeGirlsDownloader.Services;

public sealed class DownloadManager : IDownloadManager
{
    private readonly IAnimeGirlsApiClient _apiClient;
    private readonly ISelfUpdateService _selfUpdateService;
    private readonly HttpClient _updateHttpClient = new() { Timeout = Timeout.InfiniteTimeSpan };
    private readonly ObservableCollection<DownloadItem> _items = [];
    private readonly Queue<DownloadItem> _pendingItems = [];
    private readonly Dictionary<Guid, CancellationTokenSource> _cancellations = [];
    private bool _isProcessing;
    private bool _restartPending;

    public DownloadManager(IAnimeGirlsApiClient apiClient, ISelfUpdateService selfUpdateService)
    {
        _apiClient = apiClient;
        _selfUpdateService = selfUpdateService;
        _updateHttpClient.DefaultRequestHeaders.UserAgent.ParseAdd(AppConsts.AppUserAgent);
        Items = new ReadOnlyObservableCollection<DownloadItem>(_items);
    }

    public event EventHandler? QueueChanged;
    public ReadOnlyObservableCollection<DownloadItem> Items { get; }

    public DownloadItem Enqueue(string imageId, string downloadUrl, string destinationDirectory, long userId)
    {
        var item = new DownloadItem
        {
            ImageId = imageId,
            DownloadUrl = downloadUrl,
            DestinationDirectory = destinationDirectory,
            UserId = userId,
        };
        _items.Add(item);
        QueueForDownload(item);
        return item;
    }

    public DownloadItem EnqueueUpdate(UpdateCheckResult update)
    {
        if (update.Status != UpdateCheckStatus.UpdateAvailable ||
            update.DownloadUri is null ||
            string.IsNullOrWhiteSpace(update.LatestVersion))
        {
            throw new ArgumentException("The update result does not contain a downloadable update.", nameof(update));
        }

        DownloadItem? existing = _items.FirstOrDefault(item =>
            item.Kind == DownloadItemKind.ApplicationUpdate &&
            string.Equals(item.Version, update.LatestVersion, StringComparison.OrdinalIgnoreCase));
        if (existing is not null) return existing;

        var item = new DownloadItem
        {
            Kind = DownloadItemKind.ApplicationUpdate,
            DownloadUrl = update.DownloadUri.AbsoluteUri,
            DestinationDirectory = AppPaths.UpdateDownloadsDirectory,
            Version = update.LatestVersion,
            ExpectedSize = update.AssetSize,
            ExpectedDigest = update.AssetDigest,
        };
        _items.Add(item);
        QueueForDownload(item);
        return item;
    }

    public void Cancel(DownloadItem item)
    {
        if (!_cancellations.TryGetValue(item.Id, out CancellationTokenSource? cancellation)) return;

        if (item.Status == DownloadStatus.Queued)
        {
            item.Status = DownloadStatus.Canceled;
            cancellation.Cancel();
            _items.Remove(item);
            OnQueueChanged();
            return;
        }

        if (item.Status == DownloadStatus.Downloading)
        {
            item.Status = DownloadStatus.Canceling;
            cancellation.Cancel();
            OnQueueChanged();
        }
    }

    public void Retry(DownloadItem item)
    {
        if (item.Status != DownloadStatus.Failed || !_items.Contains(item)) return;
        item.ErrorMessage = null;
        item.Progress = 0;
        item.DestinationPath = string.Empty;
        item.Status = DownloadStatus.Queued;
        QueueForDownload(item);
    }

    private void QueueForDownload(DownloadItem item)
    {
        var cancellation = new CancellationTokenSource();
        _cancellations[item.Id] = cancellation;
        _pendingItems.Enqueue(item);
        OnQueueChanged();
        if (!_isProcessing) _ = ProcessQueueAsync();
    }

    private async Task ProcessQueueAsync()
    {
        if (_isProcessing) return;
        _isProcessing = true;
        OnQueueChanged();
        try
        {
            while (_pendingItems.TryDequeue(out DownloadItem? item))
            {
                if (!_cancellations.TryGetValue(item.Id, out CancellationTokenSource? cancellation))
                    continue;

                if (cancellation.IsCancellationRequested || !_items.Contains(item))
                {
                    RemoveCancellation(item.Id);
                    continue;
                }

                await DownloadOneAsync(item, cancellation.Token);
                RemoveCancellation(item.Id);
                if (_restartPending) break;
            }
        }
        finally
        {
            _isProcessing = false;
            OnQueueChanged();
        }
    }

    private async Task DownloadOneAsync(DownloadItem item, CancellationToken cancellationToken)
    {
        item.Status = DownloadStatus.Downloading;
        OnQueueChanged();
        var progress = new Progress<double>(value => item.Progress = value);
        try
        {
            if (item.Kind == DownloadItemKind.ApplicationUpdate)
            {
                item.DestinationPath = await DownloadUpdatePackageAsync(item, progress, cancellationToken);
                item.Status = DownloadStatus.PreparingUpdate;
                OnQueueChanged();
                await _selfUpdateService.PrepareAndLaunchAsync(
                    item.DestinationPath,
                    item.Version!,
                    cancellationToken);
                item.Status = DownloadStatus.Completed;
                AppLogger.LogSuccessWithInfoBar(
                    AppResourceLoader.GetString("Success_DownloadManager_UpdateReady_1"),
                    InfoBarInfoType.Auto);
                _restartPending = true;
                App.Current.ExitForUpdate();
                return;
            }

            item.DestinationPath = await _apiClient.DownloadImageAsync(
                    item.DownloadUrl,
                    item.DestinationDirectory,
                    $"{item.ImageId}.png",
                    progress,
                    cancellationToken);
            item.Status = DownloadStatus.Completed;

            string message = string.Format(
                AppResourceLoader.GetString("Success_DownloadManager_DownloadCompleted_1"),
                item.DisplayName,
                item.DestinationPath);
            AppLogger.LogSuccessWithInfoBar(message, InfoBarInfoType.Auto);
            _items.Remove(item);
        }
        catch (OperationCanceledException)
        {
            item.ErrorMessage = null;
            item.Status = DownloadStatus.Canceled;
            _items.Remove(item);
        }
        catch (Exception exception)
        {
            item.ErrorMessage = exception.Message;
            item.Status = DownloadStatus.Failed;
            string message = string.Format(
                AppResourceLoader.GetString("Error_DownloadManager_DownloadFailed_1"),
                item.DisplayName,
                exception.Message);
            AppLogger.LogErrorWithInfoBar(message, InfoBarInfoType.Auto);
        }
        finally
        {
            OnQueueChanged();
        }
    }

    private async Task<string> DownloadUpdatePackageAsync(
        DownloadItem item,
        IProgress<double> progress,
        CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(item.DestinationDirectory);
        string version = SanitizeFileName(item.Version ?? "update");
        string destinationPath = Path.Combine(item.DestinationDirectory, $"AnimeGirlsDownloader-{version}.zip");
        string temporaryPath = destinationPath + $".{item.Id:N}.partial";

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, item.DownloadUrl);
            using HttpResponseMessage response = await _updateHttpClient.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);
            response.EnsureSuccessStatusCode();

            long responseLength = response.Content.Headers.ContentLength ?? -1;
            long expectedLength = item.ExpectedSize > 0 ? item.ExpectedSize : responseLength;
            long downloaded = 0;
            using IncrementalHash hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            await using Stream source = await response.Content.ReadAsStreamAsync(cancellationToken);
            await using (var destination = new FileStream(
                temporaryPath,
                FileMode.Create,
                FileAccess.Write,
                FileShare.None,
                81920,
                FileOptions.Asynchronous | FileOptions.SequentialScan))
            {
                byte[] buffer = new byte[81920];
                while (true)
                {
                    int count = await source.ReadAsync(buffer, cancellationToken);
                    if (count == 0) break;
                    await destination.WriteAsync(buffer.AsMemory(0, count), cancellationToken);
                    hash.AppendData(buffer, 0, count);
                    downloaded += count;
                    if (expectedLength > 0)
                        progress.Report(Math.Min(100d, downloaded * 100d / expectedLength));
                }
                await destination.FlushAsync(cancellationToken);
            }

            if (item.ExpectedSize > 0 && downloaded != item.ExpectedSize)
                throw new InvalidDataException($"The update package size is invalid (expected {item.ExpectedSize}, received {downloaded}).");

            ValidateDigest(item.ExpectedDigest, hash.GetHashAndReset());
            File.Move(temporaryPath, destinationPath, overwrite: true);
            progress.Report(100);
            return destinationPath;
        }
        catch
        {
            TryDeleteFile(temporaryPath);
            throw;
        }
    }

    private static void ValidateDigest(string? expectedDigest, byte[] actualDigest)
    {
        if (string.IsNullOrWhiteSpace(expectedDigest)) return;
        const string prefix = "sha256:";
        if (!expectedDigest.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) return;

        string expected = expectedDigest[prefix.Length..].Trim();
        string actual = Convert.ToHexString(actualDigest);
        if (!string.Equals(expected, actual, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("The update package checksum does not match the GitHub release asset.");
    }

    private static string SanitizeFileName(string value)
    {
        foreach (char invalidCharacter in Path.GetInvalidFileNameChars())
            value = value.Replace(invalidCharacter, '-');
        return value;
    }

    private static void TryDeleteFile(string path)
    {
        try
        {
            if (File.Exists(path)) File.Delete(path);
        }
        catch
        {
            // A failed cleanup must not hide the original download error.
        }
    }

    private void RemoveCancellation(Guid itemId)
    {
        if (_cancellations.Remove(itemId, out CancellationTokenSource? cancellation))
            cancellation.Dispose();
    }

    private void OnQueueChanged() => QueueChanged?.Invoke(this, EventArgs.Empty);
}
