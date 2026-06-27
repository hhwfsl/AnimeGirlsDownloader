using AnimeGirlsDownloader.Interfaces;
using AnimeGirlsDownloader.Models;
using AnimeGirlsDownloader.Requests;
using AnimeGirlsDownloader.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;
using Windows.Storage.Search;

namespace AnimeGirlsDownloader
{
    public sealed partial class UploadImagePage : Page
    {
        private readonly IFileService _fileService;
        private readonly string[] _allowedExtensions = new string[] { ".jpg", ".png", ".bmp", ".jpeg", ".webp" };
        private List<Tag> _tags = new List<Tag>();
        private Uploader _uploader = new Uploader();
        private bool _isNSFW = false;
        private byte[] _data = new byte[0];
        private string? _uploaderName = null;
        private string _imagePath = string.Empty;
        private bool _isAiGenerate = false;

        private bool _isUploading = false;
        //private DispatcherTimer _timer = new DispatcherTimer();
        //private List<string> _searchedTagStrings = new List<string>();

        private Action? _closePage = null;
        public UploadImagePage()
        {
            InitializeComponent();
            _uploader.Initialize(ResultHandler);
            _uploaderName = App.Current.Services.GetService<ISettingService>()!.GetSettings().LoggedUserName;
            _fileService = App.Current.Services.GetService<IFileService>()!;
            UploadImage.Visibility = Microsoft.UI.Xaml.Visibility.Collapsed;
            DropImageTextBlock.Visibility = Microsoft.UI.Xaml.Visibility.Visible;
            UploadPageTipTeachingTip.IsOpen = false;

            //_timer.Interval = TimeSpan.FromMilliseconds(500);
            //_timer.Tick += TimerTick;
            //TagAutoSuggestBox.ItemsSource = _searchedTagStrings;

        }
        /*
        private async void TimerTick(object? sender, object e)
        {
            _timer.Stop();
            string partialTag = TagAutoSuggestBox.Text;
            if(!string.IsNullOrEmpty(partialTag))
            {
                List<Models.Tag> searchedTags = await _uploader.GetTagsByPartialTag(new TagRequest { PartialTag = partialTag})?? new List<Models.Tag>();
                List<string> searchedTagStrings = new List<string>();
                foreach(Models.Tag tag in searchedTags)
                {
                    searchedTagStrings.Add(tag.Name);
                }
                TagAutoSuggestBox.ItemsSource = searchedTagStrings;
            }
        }
        */
        public void Initialize(Action closePage)
        {
            _closePage = closePage;
        }
        private void ResultHandler(string message)
        {
            UploadPageTipTeachingTip.Subtitle = message;
            UploadPageTipTeachingTip.IsOpen = true;
        }
        private void AddTag()
        {
            string tagName = TagAutoSuggestBox.Text;
            if (string.IsNullOrEmpty(tagName) || _tags.Count >= 10 || TagContainer.Children.Count >= 10)
            {
                return;
            }
            if (_tags.Where(t => t.Name == tagName).Any())
            {
                TagAutoSuggestBox.Text = string.Empty;
                return;
            }
            _tags.Add(new Models.Tag { Name = tagName });
            TagContainer.Children.Add(new UserControls.Tag { TagText = tagName });
            TagAutoSuggestBox.Text = string.Empty;
        }
        private void AddTagButton_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            AddTag();
        }

        private void RevokeTagButton_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            if (_tags.Count > 0 && TagContainer.Children.Count > 0)
            {
                _tags.RemoveAt(_tags.Count - 1);
                TagContainer.Children.RemoveAt(TagContainer.Children.Count - 1);
            }
        }

        private void IsNSFWCheckBox_Checked(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            _isNSFW = true;
        }

        private void IsNSFWCheckBox_Unchecked(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            _isNSFW = false;
        }

        private async void UploadButton_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            if(_isUploading)
            {
                return;
            }
            if(string.IsNullOrEmpty(_imagePath))
            {
                return;
            }
            _isUploading = true;
            string creatorName = CreatorNameTextBox.Text;
            if(string.IsNullOrEmpty(creatorName))
            {
                creatorName = "Unknown";
            }
            _data = await _fileService.ImageToBytes(_imagePath);
            UploadImageRequest request = new UploadImageRequest
            {
                Creator = creatorName,
                Tags = _tags,
                IsNSFW = _isNSFW,
                Uploader = _uploaderName,
                Data = _data,
                IsAiGenerate = _isAiGenerate,
            };
            await _uploader.UploadImage(request);
            _isUploading = false;
        }
        private void ImageDragOver(Microsoft.UI.Xaml.DragEventArgs e)
        {
            if (e.DataView.Contains(StandardDataFormats.StorageItems))
            {
                e.AcceptedOperation = DataPackageOperation.Copy;
                e.DragUIOverride.Caption = AppResourceLoader.GetString("ImageDragOver");
                e.DragUIOverride.IsCaptionVisible = true;
                e.DragUIOverride.IsContentVisible = true;
            }
            else
            {
                e.AcceptedOperation = DataPackageOperation.None;
            }
        }
        private async Task<bool> ImageDrop(Microsoft.UI.Xaml.DragEventArgs e)
        {
            if (e.DataView.Contains(StandardDataFormats.StorageItems))
            {
                var deferral = e.GetDeferral();
                try
                {
                    var items = await e.DataView.GetStorageItemsAsync();
                    if (items.Count > 0 && items[0] is StorageFile file)
                    {
                        string extension = file.FileType.ToLower();
                        if (_allowedExtensions.Contains(extension))
                        {
                            UploadImage.Source = new BitmapImage(new Uri(file.Path));
                            _imagePath = file.Path;
                            return true;
                        }
                    }
                }
                catch (Exception ex)
                {
                    AppLogger.LogError(ex.Message);
                }
                finally
                {
                    deferral.Complete();
                }
            }
            return false;
        }
        private void DropImageTextBlock_DragOver(object sender, Microsoft.UI.Xaml.DragEventArgs e)
        {
            ImageDragOver(e);
        }

        private async void DropImageTextBlock_Drop(object sender, Microsoft.UI.Xaml.DragEventArgs e)
        {
            if(await ImageDrop(e))
            {
                DropImageTextBlock.Visibility = Microsoft.UI.Xaml.Visibility.Collapsed;
                UploadImage.Visibility = Microsoft.UI.Xaml.Visibility.Visible;
            }
        }

        private void UploadImage_DragOver(object sender, Microsoft.UI.Xaml.DragEventArgs e)
        {
            ImageDragOver(e);
        }

        private async void UploadImage_Drop(object sender, Microsoft.UI.Xaml.DragEventArgs e)
        {
            await ImageDrop(e);
        }

        private void CancelUploadButton_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            _closePage?.Invoke();
        }

        private void IsAiGenerateCheckBox_Checked(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            _isAiGenerate = true;
        }

        private void IsAiGenerateCheckBox_Unchecked(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            _isAiGenerate = false;
        }

        private void TagAutoSuggestBox_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
        {
            /*
            if (args.Reason == AutoSuggestionBoxTextChangeReason.UserInput)
            {
                _timer.Stop();
                _timer.Start();
            }
            */
        }

        private void TagAutoSuggestBox_QuerySubmitted(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs args)
        {
            AddTag();
        }

        private void TagAutoSuggestBox_SuggestionChosen(AutoSuggestBox sender, AutoSuggestBoxSuggestionChosenEventArgs args)
        {
            //sender.Text = args.SelectedItem.ToString();
        }

        private async void BatchUploadButton_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            var folder = await _fileService.PickFolderAsync();
            if(folder == null)
            {
                return;
            }
            var imageFiles = new List<StorageFile>();

            var queryOptions = new QueryOptions(CommonFileQuery.DefaultQuery, _allowedExtensions);

            queryOptions.FolderDepth = FolderDepth.Deep;

            var query = folder.CreateFileQueryWithOptions(queryOptions);
            var files = await query.GetFilesAsync();
            imageFiles.AddRange(
                files.Where(file => _allowedExtensions.Contains(file.FileType.ToLower()))
            );
            await BatchUpload(imageFiles);
        }
        private async Task BatchUpload(IReadOnlyList<StorageFile?>? imageFiles)
        {
            if(imageFiles == null || imageFiles.Count == 0)
            {
                return;
            }
            var parallelOptions = new ParallelOptions
            {
                MaxDegreeOfParallelism = Math.Max(1, Environment.ProcessorCount - 1)
            };
            _isUploading = true;
            await Parallel.ForEachAsync(imageFiles, parallelOptions, async (file, token) =>
            {
                if (file == null) return;
                string imagePath = file.Path;
                if (string.IsNullOrEmpty(imagePath) || !File.Exists(imagePath)) return;
                UploadImageRequest uploadImageRequest = new UploadImageRequest
                {
                    Uploader = _uploaderName,
                    IsAiGenerate = _isAiGenerate,
                    IsNSFW = _isNSFW,
                    Data = await _fileService.ImageToBytes(imagePath)
                };
                await _uploader.UploadImage(uploadImageRequest);
            });
            _isUploading = false;
        }
    }
}
