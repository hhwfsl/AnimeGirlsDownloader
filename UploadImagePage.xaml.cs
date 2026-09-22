using AnimeGirlsDownloader.Interfaces;
using AnimeGirlsDownloader.Models;
using AnimeGirlsDownloader.Requests;
using AnimeGirlsDownloader.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;

namespace AnimeGirlsDownloader;

public sealed partial class UploadImagePage : Page
{
    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    { ".jpg", ".jpeg", ".png", ".bmp", ".webp" };

    private readonly IFileService _fileService;
    private readonly IAnimeGirlsApiClient _apiClient;
    private readonly List<Tag> _tags = [];
    private CancellationTokenSource? _tagSearchCancellation;
    private Action? _closePage;
    private bool _isNSFW;
    private bool _isAiGenerate;
    private bool _isUploading;
    private bool _isTagEditingEnabled;

    public UploadImagePage()
    {
        InitializeComponent();
        _apiClient = App.Current.Services.GetRequiredService<IAnimeGirlsApiClient>();
        _fileService = App.Current.Services.GetRequiredService<IFileService>();
        UploadImage.Visibility = Visibility.Collapsed;
        SetTagEditorEnabled(false);
    }

    public ObservableCollection<UploadSourceItem> SelectedSources { get; } = [];

    public void Initialize(Action closePage) =>
        _closePage = closePage ?? throw new ArgumentNullException(nameof(closePage));

    private async void AddImagesMenuItem_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            IReadOnlyList<StorageFile> files = await _fileService.PickImagesAsync();
            foreach (StorageFile file in files) AddSource(file.Path, isFolder: false);
            UpdateSelectionState();
        }
        catch (Exception exception)
        {
            AppLogger.LogErrorWithInfoBar(exception.Message);
        }
    }

    private async void AddFolderMenuItem_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            StorageFolder? folder = await _fileService.PickFolderAsync();
            if (folder is not null) AddSource(folder.Path, isFolder: true);
            UpdateSelectionState();
        }
        catch (Exception exception)
        {
            AppLogger.LogErrorWithInfoBar(exception.Message);
        }
    }

    private void AddSource(string path, bool isFolder)
    {
        if (string.IsNullOrWhiteSpace(path)) return;
        if (!isFolder && !AllowedExtensions.Contains(Path.GetExtension(path))) return;
        if (SelectedSources.Any(item => string.Equals(item.Path, path, StringComparison.OrdinalIgnoreCase))) return;
        SelectedSources.Add(new UploadSourceItem { Path = path, IsFolder = isFolder });
    }

    private void RemoveSourceButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: UploadSourceItem item })
        {
            SelectedSources.Remove(item);
            UpdateSelectionState();
        }
    }

    private void UpdateSelectionState()
    {
        bool isSingleImage = SelectedSources.Count == 1 && !SelectedSources[0].IsFolder;
        SetTagEditorEnabled(isSingleImage);
        UploadImage.Visibility = isSingleImage ? Visibility.Visible : Visibility.Collapsed;
        PreviewPlaceholder.Visibility = isSingleImage ? Visibility.Collapsed : Visibility.Visible;
        UploadImage.Source = isSingleImage ? new BitmapImage(new Uri(SelectedSources[0].Path)) : null;

        if (!isSingleImage)
        {
            _tags.Clear();
            TagContainer.Children.Clear();
            TagAutoSuggestBox.Text = string.Empty;
        }
    }

    private void Preview_DragOver(object sender, DragEventArgs e)
    {
        e.AcceptedOperation = e.DataView.Contains(StandardDataFormats.StorageItems)
            ? DataPackageOperation.Copy
            : DataPackageOperation.None;
    }

    private async void Preview_Drop(object sender, DragEventArgs e)
    {
        if (!e.DataView.Contains(StandardDataFormats.StorageItems)) return;
        var deferral = e.GetDeferral();
        try
        {
            IReadOnlyList<IStorageItem> items = await e.DataView.GetStorageItemsAsync();
            foreach (IStorageItem item in items)
            {
                if (item is StorageFile file) AddSource(file.Path, isFolder: false);
                else if (item is StorageFolder folder) AddSource(folder.Path, isFolder: true);
            }
            UpdateSelectionState();
        }
        catch (Exception exception)
        {
            AppLogger.LogErrorWithInfoBar(exception.Message);
        }
        finally
        {
            deferral.Complete();
        }
    }

    private void AddTag()
    {
        if (!_isTagEditingEnabled) return;
        string tagName = TagAutoSuggestBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(tagName) || _tags.Count >= 10) return;
        if (_tags.Any(tag => string.Equals(tag.Name, tagName, StringComparison.OrdinalIgnoreCase)))
        {
            TagAutoSuggestBox.Text = string.Empty;
            return;
        }
        _tags.Add(new Tag { Name = tagName });
        TagContainer.Children.Add(new UserControls.Tag { TagText = tagName });
        TagAutoSuggestBox.Text = string.Empty;
    }

    private void AddTagButton_Click(object sender, RoutedEventArgs e) => AddTag();

    private void RevokeTagButton_Click(object sender, RoutedEventArgs e)
    {
        if (_tags.Count == 0) return;
        _tags.RemoveAt(_tags.Count - 1);
        TagContainer.Children.RemoveAt(TagContainer.Children.Count - 1);
    }

    private async void TagAutoSuggestBox_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
    {
        if (args.Reason != AutoSuggestionBoxTextChangeReason.UserInput || !_isTagEditingEnabled) return;
        _tagSearchCancellation?.Cancel();
        _tagSearchCancellation?.Dispose();
        _tagSearchCancellation = new CancellationTokenSource();
        CancellationToken cancellationToken = _tagSearchCancellation.Token;
        string query = sender.Text.Trim();
        if (string.IsNullOrWhiteSpace(query)) { sender.ItemsSource = null; return; }
        try
        {
            await Task.Delay(300, cancellationToken);
            sender.ItemsSource = await _apiClient.GetTagsAsync(query, cancellationToken);
        }
        catch (OperationCanceledException) { }
        catch (Exception exception) { AppLogger.LogWarning(exception.Message); }
    }

    private void TagAutoSuggestBox_QuerySubmitted(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs args) => AddTag();

    private void TagAutoSuggestBox_SuggestionChosen(AutoSuggestBox sender, AutoSuggestBoxSuggestionChosenEventArgs args)
    {
        if (args.SelectedItem is Tag tag) sender.Text = tag.Name;
    }

    private async void UploadButton_Click(object sender, RoutedEventArgs e)
    {
        if (_isUploading || SelectedSources.Count == 0) return;
        _isUploading = true;
        UploadButton.IsEnabled = false;
        int total = 0;
        int succeeded = 0;
        try
        {
            UploadSourceItem[] sources = [.. SelectedSources];
            List<string> imagePaths = await Task.Run(() => ExpandImagePaths(sources));
            total = imagePaths.Count;
            bool useTags = sources.Length == 1 && !sources[0].IsFolder;
            foreach (string imagePath in imagePaths)
            {
                try
                {
                    var request = new UploadImageRequest
                    {
                        Creator = string.IsNullOrWhiteSpace(CreatorNameTextBox.Text) ? "Unknown" : CreatorNameTextBox.Text.Trim(),
                        Tags = useTags ? [.. _tags] : null,
                        IsNSFW = _isNSFW,
                        IsAiGenerate = _isAiGenerate,
                        Data = await _fileService.EncodeImageAsPngAsync(imagePath),
                    };
                    await _apiClient.UploadImageAsync(request);
                    succeeded++;
                }
                catch (Exception exception)
                {
                    AppLogger.LogError(exception.Message);
                }
            }

            string template = AppResourceLoader.GetString("Info_UploadImagePage_BatchUpload_1");
            string message = string.Format(template, total, succeeded, total - succeeded);
            AppLogger.LogInfoWithInfoBar(message, Enums.InfoBarInfoType.Manually);
            UploadPageTipTeachingTip.Subtitle = message;
            UploadPageTipTeachingTip.IsOpen = true;
        }
        finally
        {
            _isUploading = false;
            UploadButton.IsEnabled = true;
        }
    }

    private static List<string> ExpandImagePaths(IEnumerable<UploadSourceItem> sources)
    {
        var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (UploadSourceItem source in sources)
        {
            if (!source.IsFolder)
            {
                if (File.Exists(source.Path)) paths.Add(source.Path);
                continue;
            }
            try
            {
                foreach (string path in Directory.EnumerateFiles(source.Path, "*", SearchOption.AllDirectories))
                    if (AllowedExtensions.Contains(Path.GetExtension(path))) paths.Add(path);
            }
            catch (Exception exception)
            {
                AppLogger.LogWarning($"Unable to enumerate {source.Path}: {exception.Message}");
            }
        }
        return [.. paths];
    }

    private void SetTagEditorEnabled(bool enabled)
    {
        _isTagEditingEnabled = enabled;
        TagEditorPanel.Opacity = enabled ? 1 : 0.5;
        TagAutoSuggestBox.IsEnabled = enabled;
        AddTagButton.IsEnabled = enabled;
        RevokeTagButton.IsEnabled = enabled;
    }

    private void IsNSFWCheckBox_Checked(object sender, RoutedEventArgs e) => _isNSFW = true;
    private void IsNSFWCheckBox_Unchecked(object sender, RoutedEventArgs e) => _isNSFW = false;
    private void IsAiGenerateCheckBox_Checked(object sender, RoutedEventArgs e) => _isAiGenerate = true;
    private void IsAiGenerateCheckBox_Unchecked(object sender, RoutedEventArgs e) => _isAiGenerate = false;

    private void CancelUploadButton_Click(object sender, RoutedEventArgs e)
    {
        _tagSearchCancellation?.Cancel();
        _closePage?.Invoke();
    }
}
