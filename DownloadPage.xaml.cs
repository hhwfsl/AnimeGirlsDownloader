using AnimeGirlsDownloader.Interfaces;
using AnimeGirlsDownloader.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using AnimeGirlsDownloader.Responses;

namespace AnimeGirlsDownloader;

public sealed partial class DownloadPage : Page
{
    private readonly IDownloadManager _downloadManager;
    private readonly ISettingService _settingService;
    private readonly IUserSessionService _userSessionService;
    private readonly ObservableCollection<DownloadItem> _visibleItems = [];
    private Action? _closePage;

    public DownloadPage()
    {
        InitializeComponent();
        _downloadManager = App.Current.Services.GetRequiredService<IDownloadManager>();
        _settingService = App.Current.Services.GetRequiredService<ISettingService>();
        _userSessionService = App.Current.Services.GetRequiredService<IUserSessionService>();
        DownloadListView.ItemsSource = _visibleItems;
        RefreshItems();
        ((INotifyCollectionChanged)_downloadManager.Items).CollectionChanged += Downloads_CollectionChanged;
        _userSessionService.UserChanged += UserSessionService_UserChanged;
        Unloaded += DownloadPage_Unloaded;
    }

    public void Initialize(Action closePage) =>
        _closePage = closePage ?? throw new ArgumentNullException(nameof(closePage));

    private void RefreshItems()
    {
        long userId = _settingService.GetSettings().LoggedUserId ?? 0;
        _visibleItems.Clear();
        foreach (DownloadItem item in _downloadManager.Items.Where(item => item.UserId == userId))
            _visibleItems.Add(item);
    }

    private void Downloads_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e) => RefreshItems();

    private void DownloadPage_Unloaded(object sender, RoutedEventArgs e) =>
        Unsubscribe();

    private void UserSessionService_UserChanged(object? sender, UserProfileResponse? profile) => RefreshItems();

    private void Unsubscribe()
    {
        ((INotifyCollectionChanged)_downloadManager.Items).CollectionChanged -= Downloads_CollectionChanged;
        _userSessionService.UserChanged -= UserSessionService_UserChanged;
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e) => _closePage?.Invoke();

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: DownloadItem item }) _downloadManager.Cancel(item);
    }

    private void RetryButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: DownloadItem item }) _downloadManager.Retry(item);
    }
}
