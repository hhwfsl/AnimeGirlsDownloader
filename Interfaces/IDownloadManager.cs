using AnimeGirlsDownloader.Models;
using System;
using System.Collections.ObjectModel;

namespace AnimeGirlsDownloader.Interfaces;

public interface IDownloadManager
{
    event EventHandler? QueueChanged;
    ReadOnlyObservableCollection<DownloadItem> Items { get; }
    DownloadItem Enqueue(string imageId, string downloadUrl, string destinationDirectory, long userId);
    DownloadItem EnqueueUpdate(UpdateCheckResult update);
    void Cancel(DownloadItem item);
    void Retry(DownloadItem item);
}
