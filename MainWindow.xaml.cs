using AnimeGirlsDownloader.Enums;
using AnimeGirlsDownloader.Interfaces;
using AnimeGirlsDownloader.Models;
using AnimeGirlsDownloader.Responses;
using AnimeGirlsDownloader.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media.Imaging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Windows.ApplicationModel.DataTransfer;
using Windows.Graphics;
using Windows.Storage;
using Windows.Storage.Streams;
using Windows.System;
using Windows.UI;


namespace AnimeGirlsDownloader
{
    public sealed partial class MainWindow : Window
    {
        // Window
        private IntPtr _hWnd;
        private WindowId _windowId;
        private int _windowWidth;
        private int _windowHeight;
        private SizeInt32 _windowDefaultSize;

        // Data
        private GetImageResponse? _currentImage;
        private string _imageId = string.Empty;
        private readonly CancellationTokenSource _lifetimeCancellation = new();
        private bool _isGettingImage;
        private TaskCompletionSource<bool>? _imageLoadCompletion;

        // Page
        private UploadImagePage? _uploadImagePage;
        private bool _isUploadImagePageOpen;
        private bool _isDownloadPageOpen;

        private readonly IAnimeGirlsApiClient _apiClient;
        private readonly IFileService _fileService;
        private readonly ISettingService _settingService;
        private readonly IUserSessionService _userSessionService;
        private readonly IDownloadManager _downloadManager;
        private readonly IUpdateService _updateService;
        private bool IsContentPageOpen => _isUploadImagePageOpen || _isDownloadPageOpen;
        public MainWindow()
        {
            InitializeComponent();
            _apiClient = App.Current.Services.GetRequiredService<IAnimeGirlsApiClient>();
            _fileService = App.Current.Services.GetRequiredService<IFileService>();
            _settingService = App.Current.Services.GetRequiredService<ISettingService>();
            _userSessionService = App.Current.Services.GetRequiredService<IUserSessionService>();
            _downloadManager = App.Current.Services.GetRequiredService<IDownloadManager>();
            _updateService = App.Current.Services.GetRequiredService<IUpdateService>();
            Initialize();
        }
        private void Initialize()
        {
            InitializeWindow();
            _fileService.Initialize(this);
            SizeChanged += MainWindow_SizeChanged;
            _settingService.Initialize(ResizeWindowToStandardSize);

            UploadImagePageGrid.Visibility = Visibility.Collapsed;
            DownloadPageGrid.Visibility = Visibility.Collapsed;
            DisplayImageGrid.Visibility = Visibility.Visible;
            ImageLoadingProgressRing.IsActive = false;
            ImageLoadingProgressRing.Visibility = Visibility.Collapsed;
            ClearCurrentImage();
            AppLogger.Initialize(AddMessageToQueue);

            Closed += WindowClosed;
            _userSessionService.UserChanged += UserSessionService_UserChanged;
            _downloadManager.QueueChanged += DownloadManager_QueueChanged;
            UpdateDownloadQueueBadge();
            _ = InitializeLoginAsync();
        }
        /// <summary>
        /// Save window size when window size changed.
        /// </summary>
        /// <param name="sender">_</param>
        /// <param name="args">_</param>
        private void MainWindow_SizeChanged(object sender, WindowSizeChangedEventArgs args)
        {
            _windowWidth = AppWindow.Size.Width;
            _windowHeight = AppWindow.Size.Height;
        }

        private async void MainGrid_Loaded(object sender, RoutedEventArgs e)
        {
            MainGrid.Loaded -= MainGrid_Loaded;
            try
            {
                UpdateCheckResult result = await _updateService.CheckForUpdateAsync();
                if (result.Status == UpdateCheckStatus.UpdateAvailable && MainGrid.XamlRoot is not null)
                    await UpdatePrompt.ShowAsync(MainGrid.XamlRoot, result);
            }
            catch
            {
                // Startup update checks are intentionally silent and must never block the app.
            }
        }
        /// <summary>
        /// Initialize window properties.
        /// </summary>
        private void InitializeWindow()
        {
            var presenter = AppWindow.Presenter as OverlappedPresenter;
            presenter!.SetBorderAndTitleBar(true, false);
            this.ExtendsContentIntoTitleBar = true;
            SetTitleBar(TitleBarGrid);
            _hWnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
            _windowId = Win32Interop.GetWindowIdFromWindow(_hWnd);

            InitializeWindowSize();
            InitializeWindowPosition();
            InitializeWindowTheme();
            
        }
        /// <summary>
        /// Initialize window position to center of the screen.
        /// </summary>
        private void InitializeWindowPosition()
        {
            DisplayArea displayArea = DisplayArea.GetFromWindowId(_windowId, DisplayAreaFallback.Primary);
            int x = displayArea.WorkArea.X + (displayArea.WorkArea.Width - AppWindow.Size.Width) / 2;
            int y = displayArea.WorkArea.Y + (displayArea.WorkArea.Height - AppWindow.Size.Height) / 2;
            AppWindow.Move(new PointInt32(x, y));
        }
        /// <summary>
        /// Initialize window size from settings or set to standard size.
        /// </summary>
        private void InitializeWindowSize()
        {
            _windowDefaultSize = AppWindow.Size;
            Settings settings = _settingService.GetSettings();
            if (settings.WindowWidth != 0 && settings.WindowHeight != 0)
            {
                _windowWidth = settings.WindowWidth;
                _windowHeight = settings.WindowHeight;
                AppWindow.Resize(new SizeInt32(_windowWidth, _windowHeight));
            }
            else
            {
                ResizeWindowToStandardSize();
            }
        }
        /// <summary>
        /// Initialize window theme from settings.
        /// </summary>
        private void InitializeWindowTheme()
        {
            Settings settings = _settingService.GetSettings();
            MainGrid.RequestedTheme = settings.AppTheme;
        }
        private async Task InitializeLoginAsync()
        {
            try
            {
                bool isLoginSuccessful = await _apiClient.LoginWithStoredTokenAsync(_lifetimeCancellation.Token);
                if (!isLoginSuccessful)
                {
                    _settingService.DeactivateUser();
                    AppLogger.LogWarningWithInfoBar(
                        AppResourceLoader.GetString("Warning_MainWindow_InitializeLogin_1"),
                        InfoBarInfoType.Auto);
                    return;
                }

                UserProfileResponse profile = await _userSessionService.SynchronizeAsync(_lifetimeCancellation.Token);

                string info = string.Format(
                    AppResourceLoader.GetString("Success_MainWindow_InitializeLogin_1"),
                    profile.Name);
                AppLogger.LogSuccessWithInfoBar(info, InfoBarInfoType.Auto);
            }
            catch (ApiClientException exception)
            {
                AppLogger.LogWarningWithInfoBar(exception.Message, InfoBarInfoType.Auto);
            }
            catch (OperationCanceledException) when (_lifetimeCancellation.IsCancellationRequested)
            {
            }
            finally
            {
                if (!_lifetimeCancellation.IsCancellationRequested)
                    await GetRandomImageAsync(null);
            }
        }
        private void ResizeWindowToStandardSize()
        {
            //DisplayArea displayArea = DisplayArea.GetFromWindowId(_windowId, DisplayAreaFallback.Primary);
            _windowWidth = _windowDefaultSize.Width;
            _windowHeight = _windowDefaultSize.Height;
            _settingService
                .SetWindowWidth(0)
                .SetWindowHeight(0)
                .SaveSetting();
            AppWindow.Resize(new SizeInt32(_windowWidth, _windowHeight));
            InitializeWindowPosition();
        }
        private void AddMessageToQueue(InfoBarSeverity status, string message, InfoBarInfoType type)
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                if (type == InfoBarInfoType.Manually)
                {
                    ShowPersistentInfoBar(status, message);
                }
                else
                {
                    ShowTemporaryInfoBar(status, message);
                }
            });
        }
        
        private void WindowClosed(object? sender, WindowEventArgs args)
        {
            _userSessionService.UserChanged -= UserSessionService_UserChanged;
            _downloadManager.QueueChanged -= DownloadManager_QueueChanged;
            _settingService
                .SetWindowWidth(_windowWidth)
                .SetWindowHeight(_windowHeight)
                .SaveSetting();
            _lifetimeCancellation.Cancel();
            _lifetimeCancellation.Dispose();
        }

        private void UserSessionService_UserChanged(object? sender, UserProfileResponse? profile) =>
            DispatcherQueue.TryEnqueue(() =>
            {
                InitializeWindowTheme();
                UpdateDownloadQueueBadge();
            });

        private void DownloadManager_QueueChanged(object? sender, EventArgs e) =>
            DispatcherQueue.TryEnqueue(UpdateDownloadQueueBadge);

        private void UpdateDownloadQueueBadge()
        {
            long userId = _settingService.GetSettings().LoggedUserId ?? 0;
            DownloadItem[] items = _downloadManager.Items.Where(item => item.UserId == userId).ToArray();
            if (items.Length == 0)
            {
                DownloadQueueBadge.Visibility = Visibility.Collapsed;
                return;
            }

            bool hasActiveTask = items.Any(item => item.Status is
                DownloadStatus.Queued or DownloadStatus.Downloading or DownloadStatus.Canceling);
            bool hasFailedTask = items.Any(item => item.Status == DownloadStatus.Failed);
            DownloadQueueBadgeText.Text = !hasActiveTask && hasFailedTask
                ? "×"
                : items.Length > 99 ? "99+" : items.Length.ToString();
            DownloadQueueBadge.Visibility = Visibility.Visible;
        }
        private void ShowPersistentInfoBar(InfoBarSeverity severityLevel, string message)
        {
            InfoBar infoBar = new InfoBar();
            infoBar.IsOpen = true;
            infoBar.Severity = severityLevel;
            infoBar.Message = message;
            infoBar.Title = severityLevel.ToString();
            infoBar.IsClosable = true;
            infoBar.HorizontalAlignment = HorizontalAlignment.Right;
            infoBar.VerticalAlignment = VerticalAlignment.Bottom;
            infoBar.Background = severityLevel switch
            {
                InfoBarSeverity.Informational => new Microsoft.UI.Xaml.Media.SolidColorBrush(Colors.LightBlue),
                InfoBarSeverity.Success => new Microsoft.UI.Xaml.Media.SolidColorBrush(Colors.LightGreen),
                InfoBarSeverity.Warning => new Microsoft.UI.Xaml.Media.SolidColorBrush(Colors.LightGoldenrodYellow),
                InfoBarSeverity.Error => new Microsoft.UI.Xaml.Media.SolidColorBrush(Colors.LightCoral),
                _ => new Microsoft.UI.Xaml.Media.SolidColorBrush(Colors.LightGray),
            };
            infoBar.Transitions = new Microsoft.UI.Xaml.Media.Animation.TransitionCollection
            {
                new Microsoft.UI.Xaml.Media.Animation.EntranceThemeTransition(),
            };
            InfoBarGrid.Children.Add(infoBar);
        }
        private async void ShowTemporaryInfoBar(InfoBarSeverity severityLevel, string message)
        {
            InfoBar infoBar = new InfoBar();
            infoBar.IsOpen = true;
            infoBar.Severity = severityLevel;
            infoBar.Message = message;
            infoBar.Title = severityLevel.ToString();
            infoBar.IsClosable = true;
            infoBar.HorizontalAlignment = HorizontalAlignment.Right;
            infoBar.VerticalAlignment = VerticalAlignment.Bottom;
            infoBar.Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Colors.Black);
            infoBar.Background = severityLevel switch
            {
                InfoBarSeverity.Informational => new Microsoft.UI.Xaml.Media.SolidColorBrush(Color.FromArgb(255, 0, 162, 255)),
                InfoBarSeverity.Success => new Microsoft.UI.Xaml.Media.SolidColorBrush(Color.FromArgb(255, 0, 202, 78)),
                InfoBarSeverity.Warning => new Microsoft.UI.Xaml.Media.SolidColorBrush(Color.FromArgb(255, 255, 189, 68)),
                InfoBarSeverity.Error => new Microsoft.UI.Xaml.Media.SolidColorBrush(Color.FromArgb(255, 255, 96, 92)),
                _ => new Microsoft.UI.Xaml.Media.SolidColorBrush(Color.FromArgb(255, 0, 162, 255)),
            };
            infoBar.Transitions = new Microsoft.UI.Xaml.Media.Animation.TransitionCollection
            {
                new Microsoft.UI.Xaml.Media.Animation.EntranceThemeTransition(),
            };
            InfoBarGrid.Children.Add(infoBar);
            await Task.Delay(2500);
            infoBar.IsOpen = false;
            InfoBarGrid.Children.Remove(infoBar);
        }
        private async Task DisplayImageResponseAsync(GetImageResponse response)
        {
            var completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            _imageLoadCompletion = completion;
            var bitmap = new BitmapImage
            {
                CreateOptions = BitmapCreateOptions.IgnoreImageCache,
                UriSource = new Uri(response.PreviewUrl, UriKind.Absolute),
            };
            DisplayImage.Source = bitmap;
            try
            {
                await completion.Task.WaitAsync(_lifetimeCancellation.Token);
                _currentImage = response;
                _imageId = response.Id;
                SetCurrentImageActionsEnabled(true);
            }
            finally
            {
                if (ReferenceEquals(_imageLoadCompletion, completion))
                    _imageLoadCompletion = null;
            }
        }

        private void DisplayImage_ImageOpened(object sender, RoutedEventArgs e) =>
            _imageLoadCompletion?.TrySetResult(true);

        private void DisplayImage_ImageFailed(object sender, ExceptionRoutedEventArgs e) =>
            _imageLoadCompletion?.TrySetException(new InvalidOperationException(e.ErrorMessage));

        private void ClearCurrentImage()
        {
            _currentImage = null;
            _imageId = string.Empty;
            DisplayImage.Source = null;
            SetCurrentImageActionsEnabled(false);
        }

        private void SetCurrentImageActionsEnabled(bool isEnabled)
        {
            SaveImageButton.IsEnabled = isEnabled;
            CopyImageMenuFlyoutItem.IsEnabled = isEnabled;
            CopyImageLinkMenuFlyoutItem.IsEnabled = isEnabled;
        }

        private async Task GetRandomImageAsync(List<Tag>? tags)
        {
            if (_isGettingImage)
            {
                AppLogger.LogInfoWithInfoBar(AppResourceLoader.GetString("Error_MainWindow_GetRandomImage_1"), InfoBarInfoType.Auto);
                return;
            }
            _isGettingImage = true;
            ClearCurrentImage();
            try
            {
                DisplayImageGrid.Visibility = Visibility.Collapsed;
                if (!IsContentPageOpen)
                {
                    ImageLoadingProgressRing.Visibility = Visibility.Visible;
                    ImageLoadingProgressRing.IsActive = true;
                }

                Settings settings = _settingService.GetSettings();
                GetImageResponse response = await _apiClient.GetRandomImageAsync(
                    tags,
                    settings.ImageType,
                    settings.IsAllowAiGenerated,
                    _lifetimeCancellation.Token);
                await DisplayImageResponseAsync(response);
            }
            catch (OperationCanceledException) when (_lifetimeCancellation.IsCancellationRequested)
            {
            }
            catch (ApiClientException exception)
            {
                AppLogger.LogErrorWithInfoBar(exception.Message);
            }
            catch (InvalidOperationException exception)
            {
                string message = string.Format(
                    AppResourceLoader.GetString("Error_MainWindow_LoadPreview_1"),
                    exception.Message);
                AppLogger.LogErrorWithInfoBar(message);
            }
            finally
            {
                ImageLoadingProgressRing.Visibility = Visibility.Collapsed;
                ImageLoadingProgressRing.IsActive = false;
                if (!IsContentPageOpen)
                {
                    DisplayImageGrid.Visibility = Visibility.Visible;
                }

                _isGettingImage = false;
            }
        }

        private async Task GetImageByIdAsync(long id)
        {
            if (_isGettingImage)
            {
                AppLogger.LogInfoWithInfoBar(AppResourceLoader.GetString("Error_MainWindow_GetRandomImage_1"), InfoBarInfoType.Auto);
                return;
            }
            _isGettingImage = true;
            ClearCurrentImage();
            try
            {
                DisplayImageGrid.Visibility = Visibility.Collapsed;
                if (!IsContentPageOpen)
                {
                    ImageLoadingProgressRing.Visibility = Visibility.Visible;
                    ImageLoadingProgressRing.IsActive = true;
                }

                GetImageResponse response = await _apiClient.GetImageByIdAsync(id, _lifetimeCancellation.Token);
                await DisplayImageResponseAsync(response);
            }
            catch (OperationCanceledException) when (_lifetimeCancellation.IsCancellationRequested)
            {
            }
            catch (ApiClientException exception)
            {
                AppLogger.LogErrorWithInfoBar(exception.Message);
            }
            catch (InvalidOperationException exception)
            {
                string message = string.Format(
                    AppResourceLoader.GetString("Error_MainWindow_LoadPreview_1"),
                    exception.Message);
                AppLogger.LogErrorWithInfoBar(message);
            }
            finally
            {
                ImageLoadingProgressRing.Visibility = Visibility.Collapsed;
                ImageLoadingProgressRing.IsActive = false;
                if (!IsContentPageOpen)
                    DisplayImageGrid.Visibility = Visibility.Visible;
                _isGettingImage = false;
            }
        }
        private void RefreshImage(string query)
        {
            ProcessTagsInputAndGetImage(query);
        }
        private void ProcessTagsInputAndGetImage(string? query)
        {
            if (string.IsNullOrEmpty(query))
            {
                _ = GetRandomImageAsync(null);
                return;
            }
            string[] tagStrings = query.Split(
                ' ',
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (tagStrings.Length == 0)
            {
                _ = GetRandomImageAsync(null);
                return;
            }

            if (tagStrings.Length == 1 && long.TryParse(tagStrings[0], out long imageId))
            {
                _ = GetImageByIdAsync(imageId);
                return;
            }
            List<Models.Tag> tags = new List<Models.Tag>();
            foreach (string tag in tagStrings)
            {
                tags.Add(new Models.Tag { Name = tag });
            }
            _ = GetRandomImageAsync(tags);
        }
        private async Task SaveImageAsync()
        {
            string savingPath = _settingService.GetSettings().SavingPath ?? string.Empty;
            bool isFixedSavingPath = _settingService.GetSettings().IsEnableFixedSavingPath;
            if (_currentImage is null)
            {
                AppLogger.LogErrorWithInfoBar(AppResourceLoader.GetString("Error_MainWindow_SaveImage_1"));
                return;
            }
            if (!isFixedSavingPath)
            {
                StorageFolder? folder = await _fileService.PickFolderAsync();
                if (folder == null)
                {
                    return;
                }
                savingPath = folder.Path;
            }
            else if (string.IsNullOrWhiteSpace(savingPath))
            {
                AppLogger.LogErrorWithInfoBar(AppResourceLoader.GetString("Error_MainWindow_OpenSavingPathButtonClick_1"));
                return;
            }

            long userId = _settingService.GetSettings().LoggedUserId ?? 0;
            _downloadManager.Enqueue(_currentImage.Id, _currentImage.DownloadUrl, savingPath, userId);
        }

        private void CopyImageToClipBoard()
        {
            if (_currentImage is null)
            {
                AppLogger.LogErrorWithInfoBar(AppResourceLoader.GetString("Error_MainWindow_CopyImageToClipBoard_1"));
                return;
            }
            try
            {
                var streamReference = RandomAccessStreamReference.CreateFromUri(new Uri(_currentImage.PreviewUrl));
                var dataPackage = new DataPackage();
                dataPackage.SetBitmap(streamReference);
                Clipboard.SetContent(dataPackage);
                Clipboard.Flush();
                AppLogger.LogSuccessWithInfoBar(AppResourceLoader.GetString("Success_MainWindow_CopyImageToClipBoard_1"), InfoBarInfoType.Auto);
            }
            catch (Exception exception)
            {
                AppLogger.LogError(exception.Message);
                AppLogger.LogErrorWithInfoBar(AppResourceLoader.GetString("Error_MainWindow_CopyImageToClipBoard_2"));
            }

        }
        private void CopyImageLinkToClipboard()
        {
            if (string.IsNullOrWhiteSpace(_imageId))
            {
                AppLogger.LogErrorWithInfoBar(AppResourceLoader.GetString("Error_MainWindow_CopyImageLinkToClipBoard_1"));
                return;
            }

            try
            {
                string imageLink = $"{AppConsts.AnimeGirlsImageIdLinkEndpoint}{_imageId}";
                var dataPackage = new DataPackage();
                dataPackage.SetText(imageLink);
                Clipboard.SetContent(dataPackage);
                Clipboard.Flush();
                AppLogger.LogSuccessWithInfoBar(AppResourceLoader.GetString("Success_MainWindow_CopyImageLinkToClipBoard_1"), InfoBarInfoType.Auto);
            }
            catch (Exception exception)
            {
                AppLogger.LogError(exception.Message);
                AppLogger.LogErrorWithInfoBar(AppResourceLoader.GetString("Error_MainWindow_CopyImageLinkToClipBoard_1"));
            }
        }

        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
        [DllImport("user32.dll")]
        static extern bool IsZoomed(IntPtr hWnd);


        private void WindowControlCloseEllipse_PointerReleased(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            Close();
        }

        private void WindowControlMinimizeEllipse_PointerReleased(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            const int SW_MINIMIZE = 6;
            ShowWindow(_hWnd, SW_MINIMIZE);
        }

        private void WindowControlMaximizeEllipse_PointerReleased(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        {

            if (IsZoomed(_hWnd))
            {
                const int SW_RESTORE = 9;
                ShowWindow(_hWnd, SW_RESTORE);
            }
            else
            {
                const int SW_MAXIMIZE = 3;
                ShowWindow(_hWnd, SW_MAXIMIZE);
            }
        }

        private void SettingButton_Click(object sender, RoutedEventArgs e)
        {
            Button button = (Button)sender;
            Frame frame = new Frame();
            frame.Navigate(typeof(SettingPage));

            Flyout flyout = new Flyout
            {
                Placement = FlyoutPlacementMode.Left,
                Content = frame,
            };
            flyout.ShowAt(button);
        }
        private void ChangeThemeButton_Click(object sender, RoutedEventArgs e)
        {
            MainGrid.RequestedTheme = MainGrid.RequestedTheme == ElementTheme.Dark ? ElementTheme.Light : ElementTheme.Dark;
            _settingService
                .SetAppTheme(MainGrid.RequestedTheme)
                .SaveSetting();
        }

        private async void OpenSavingPathButton_Click(object sender, RoutedEventArgs e)
        {
            string path = _settingService.GetSettings().SavingPath ?? string.Empty;
            if (string.IsNullOrEmpty(path))
            {
                AppLogger.LogErrorWithInfoBar(AppResourceLoader.GetString("Error_MainWindow_OpenSavingPathButtonClick_1"));
                return;
            }
            try
            {
                if (!Directory.Exists(path))
                {
                    Directory.CreateDirectory(path);
                }
                StorageFolder folder = await StorageFolder.GetFolderFromPathAsync(path);
                await Launcher.LaunchFolderAsync(folder);
            }
            catch (Exception exception)
            {
                AppLogger.LogErrorWithInfoBar(exception.Message);
            }
        }

        private void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            string query = SearchTagsAutoSuggestBox.Text;
            RefreshImage(query);
        }

        private async void SaveImageButton_Click(object sender, RoutedEventArgs e)
        {
            await SaveImageAsync();
        }

        private void CopyImageMenuFlyoutItem_Click(object sender, RoutedEventArgs e)
        {
            CopyImageToClipBoard();
        }

        private void UploadImageButton_Click(object sender, RoutedEventArgs e)
        {
            if (_isUploadImagePageOpen) return;
            CloseDownloadPage();
            _uploadImagePage = new UploadImagePage();
            _uploadImagePage.Initialize(CloseUploadImagePage);
            DisplayImageGrid.Visibility = Visibility.Collapsed;
            ImageLoadingProgressRing.Visibility = Visibility.Collapsed;
            UploadImagePageFrame.Content = _uploadImagePage;
            UploadImagePageGrid.Visibility = Visibility.Visible;
            _isUploadImagePageOpen = true;
        }
        private void CloseUploadImagePage()
        {
            if (!_isUploadImagePageOpen) return;
            UploadImagePageGrid.Visibility = Visibility.Collapsed;
            UploadImagePageFrame.Content = null;
            _uploadImagePage = null;
            _isUploadImagePageOpen = false;
            if(_isGettingImage)
            {
                ImageLoadingProgressRing.Visibility = Visibility.Visible;
            }
            else
            {
                DisplayImageGrid.Visibility = Visibility.Visible;
            }
        }

        private void DownloadQueueButton_Click(object sender, RoutedEventArgs e) => OpenDownloadPage();

        private void OpenDownloadPage()
        {
            if (_isDownloadPageOpen) return;
            CloseUploadImagePage();
            var page = new DownloadPage();
            page.Initialize(CloseDownloadPage);
            DisplayImageGrid.Visibility = Visibility.Collapsed;
            ImageLoadingProgressRing.Visibility = Visibility.Collapsed;
            DownloadPageFrame.Content = page;
            DownloadPageGrid.Visibility = Visibility.Visible;
            _isDownloadPageOpen = true;
        }

        private void CloseDownloadPage()
        {
            if (!_isDownloadPageOpen) return;
            DownloadPageGrid.Visibility = Visibility.Collapsed;
            DownloadPageFrame.Content = null;
            _isDownloadPageOpen = false;
            if (_isGettingImage)
                ImageLoadingProgressRing.Visibility = Visibility.Visible;
            else
                DisplayImageGrid.Visibility = Visibility.Visible;
        }

        private void SearchTagsAutoSuggestBox_QuerySubmitted(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs args)
        {
            string query = sender.Text;
            RefreshImage(query);
        }

        private void CopyImageLinkMenuFlyoutItem_Click(object sender, RoutedEventArgs e)
        {
            CopyImageLinkToClipboard();
        }

    }
}
