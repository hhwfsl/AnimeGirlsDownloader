using AnimeGirlsDownloader.Enums;
using AnimeGirlsDownloader.Interfaces;
using AnimeGirlsDownloader.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;

namespace AnimeGirlsDownloader.Services;

public sealed class DownloadManager : IDownloadManager
{
    private readonly IAnimeGirlsApiClient _apiClient;
    private readonly ObservableCollection<DownloadItem> _items = [];
    private readonly Queue<DownloadItem> _pendingItems = [];
    private readonly Dictionary<Guid, CancellationTokenSource> _cancellations = [];
    private bool _isProcessing;

    public DownloadManager(IAnimeGirlsApiClient apiClient)
    {
        _apiClient = apiClient;
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

    private void RemoveCancellation(Guid itemId)
    {
        if (_cancellations.Remove(itemId, out CancellationTokenSource? cancellation))
            cancellation.Dispose();
    }

    private void OnQueueChanged() => QueueChanged?.Invoke(this, EventArgs.Empty);
}
