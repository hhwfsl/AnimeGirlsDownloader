using AnimeGirlsDownloader.Enums;
using AnimeGirlsDownloader.Interfaces;
using AnimeGirlsDownloader.Models;
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
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Windows.ApplicationModel.DataTransfer;
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

        // Data
        private Downloader _downloader;
        private byte[]? _imageBytes = null;
        private string _imageId = string.Empty;
        private Queue<InfoMessage> _infoMessageQueue = new Queue<InfoMessage>();
        // Control
        private SemaphoreSlim _infoQueueSemaphore = new SemaphoreSlim(0);
        private bool _isWindowClosing = false;
        private bool _isGettingImage = false;

        // Page
        private UploadImagePage? _uploadImagePage = null;
        private bool _isUploadImagePageOpen = false;

        private IFileService _fileService;
        private ISettingService _settingService;
        public MainWindow()
        {
            InitializeComponent();
            _fileService = App.Current.Services.GetService<IFileService>()!;
            _settingService = App.Current.Services.GetService<ISettingService>()!;
            Initialize();
            _downloader = new Downloader(BytesToBitmapImage);
            GetRandomImage(null);
        }
        private void Initialize()
        {
            InitializeWindow();
            _fileService!.Initialize(this);
            this.SizeChanged += MainWindow_SizeChanged;
            _settingService.Initialize(ResizeWindowToStandardSize);

            UploadImagePageGrid.Visibility = Visibility.Collapsed;
            DisplayImageGrid.Visibility = Visibility.Visible;
            ImageLoadingProgressRing.IsActive = false;
            ImageLoadingProgressRing.Visibility = Visibility.Collapsed;
            AppLogger.Initialize(AddErrorAndWarningMessageToQueue);

            this.Closed += WindowClosed;

            Task.Run(InfomationInfo);
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
            _settingService
                    .SetWindowWidth(_windowWidth)
                    .SetWindowHeight(_windowHeight)
                    .SaveSetting();
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
            AppWindow.Move(new Windows.Graphics.PointInt32(x, y));
        }
        /// <summary>
        /// Initialize window size from settings or set to standard size.
        /// </summary>
        private void InitializeWindowSize()
        {
            Settings settings = _settingService.GetSettings();
            if (settings.WindowWidth != 0 && settings.WindowHeight != 0)
            {
                _windowWidth = settings.WindowWidth;
                _windowHeight = settings.WindowHeight;
                AppWindow.Resize(new Windows.Graphics.SizeInt32(_windowWidth, _windowHeight));
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
        private void ResizeWindowToStandardSize()
        {
            DisplayArea displayArea = DisplayArea.GetFromWindowId(_windowId, DisplayAreaFallback.Primary);
            _windowWidth = displayArea.WorkArea.Width / 2;
            _windowHeight = displayArea.WorkArea.Height / 3 * 2;
            _settingService
                .SetWindowWidth(_windowWidth)
                .SetWindowHeight(_windowHeight)
                .SaveSetting();
            AppWindow.Resize(new Windows.Graphics.SizeInt32(_windowWidth, _windowHeight));
        }
        private void AddErrorAndWarningMessageToQueue(InfoBarSeverity status, string message, InfoBarInfoType type)
        {
            InfoMessage infoMessage = new InfoMessage
            {
                Message = message,
                InfoBarType = type,
                Severity = status
            };
            _infoMessageQueue.Enqueue(infoMessage);
            _infoQueueSemaphore.Release();
        }
        private async Task InfomationInfo()
        {
            while (!_isWindowClosing)
            {
                await _infoQueueSemaphore.WaitAsync();
                if (_infoMessageQueue.Count > 0)
                {
                    InfoMessage infoMessage = _infoMessageQueue.Dequeue();
                    if(infoMessage.InfoBarType == InfoBarInfoType.Manually)
                    {
                        DispatcherQueue.TryEnqueue(() => InfoManuallyClose(infoMessage.Severity, infoMessage.Message));
                    }
                    else
                    {
                        DispatcherQueue.TryEnqueue(() => InfoAutoClose(infoMessage.Severity, infoMessage.Message));
                    }
                }
                await Task.Delay(200);
            }
        }
        
        private void WindowClosed(object? sender, WindowEventArgs args)
        {
            _isWindowClosing = true;
        }
        private void InfoManuallyClose(InfoBarSeverity severityLevel, string message)
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
        private async void InfoAutoClose(InfoBarSeverity severityLevel, string message)
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
        private void BytesToBitmapImage(byte[]? bytes, string id, List<Models.Tag>? tags)
        {
            if (bytes == null)
            {
                AppLogger.LogErrorWithInfoBar(AppResourceLoader.GetString("Error_MainWindow_BytesToBitmapImage_1"));
                return;
            }
            BitmapImage bitmapImage = new BitmapImage();
            using (MemoryStream ms = new MemoryStream(bytes))
            {
                bitmapImage.SetSource(ms.AsRandomAccessStream());
                DisplayImage.Source = bitmapImage;
                _imageBytes = bytes;
                _imageId = id;
                //ImageIdTextBlock.Text = _imageId;
            }
            //TagContainer.Children.Clear();
            if(tags != null)
            {
                //foreach (Models.Tag tag in tags)
                //{
                //    TagContainer.Children.Add(new UserControls.Tag { TagText = tag.Name });
                //}
            }
        }

        private async void GetRandomImage(List<Tag>? tags)
        {
            if (_isGettingImage)
            {
                AddErrorAndWarningMessageToQueue(InfoBarSeverity.Informational, AppResourceLoader.GetString("Error_MainWindow_GetRandomImage_1"), InfoBarInfoType.Auto);
                return;
            }
            _isGettingImage = true;

            DisplayImageGrid.Visibility = Visibility.Collapsed;
            if (!_isUploadImagePageOpen)
            {
                
                ImageLoadingProgressRing.Visibility = Visibility.Visible;
                ImageLoadingProgressRing.IsActive = true;
            }
            

            Settings settings = _settingService.GetSettings();
            await _downloader.GetRandomImage(tags, settings.ImageType, settings.IsAllowAiGenerated);

            ImageLoadingProgressRing.Visibility = Visibility.Collapsed;
            ImageLoadingProgressRing.IsActive = false;
            if (!_isUploadImagePageOpen)
            {
                DisplayImageGrid.Visibility = Visibility.Visible;
            }
            _isGettingImage = false;
        }
        private async void GetImageById(long id)
        {
            if (_isGettingImage)
            {
                AddErrorAndWarningMessageToQueue(InfoBarSeverity.Informational, AppResourceLoader.GetString("Error_MainWindow_GetRandomImage_1"), InfoBarInfoType.Auto);
                return;
            }
            _isGettingImage = true;
            DisplayImageGrid.Visibility = Visibility.Collapsed;
            ImageLoadingProgressRing.Visibility = Visibility.Visible;
            ImageLoadingProgressRing.IsActive = true;

            Settings settings = _settingService.GetSettings();
            await _downloader.GetImageById(id);

            ImageLoadingProgressRing.Visibility = Visibility.Collapsed;
            ImageLoadingProgressRing.IsActive = false;
            DisplayImageGrid.Visibility = Visibility.Visible;
            _isGettingImage = false;
        }
        private void ProcessTagsInputAndGetImage(string? query)
        {
            if (string.IsNullOrEmpty(query))
            {
                GetRandomImage(null);
                return;
            }
            string[] tagStrings = query.Split(' ');
            if (tagStrings.Length == 1)
            {
                try
                {
                    long id = Convert.ToInt64(tagStrings[0]);
                    GetImageById(id);
                }
                catch { }
            }
            List<Models.Tag> tags = new List<Models.Tag>();
            foreach (string tag in tagStrings)
            {
                tags.Add(new Models.Tag { Name = tag });
            }
            GetRandomImage(tags);
        }
        private async void SaveImage()
        {
            string savingPath = _settingService.GetSettings().SavingPath ?? string.Empty;
            bool isFixedSavingPath = _settingService.GetSettings().IsEnableFixedSavingPath;
            if (_imageBytes == null)
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
            savingPath = Path.Combine(savingPath, $"{_imageId}.png");
            try
            {
                await File.WriteAllBytesAsync(savingPath, _imageBytes);
            }
            catch (Exception e)
            {
                AppLogger.LogErrorWithInfoBar(e.Message);
            }
            AppLogger.LogInfo($"{AppResourceLoader.GetString("Info_MainWindow_SaveImage_1")}{savingPath}");
            AddErrorAndWarningMessageToQueue(InfoBarSeverity.Success, AppResourceLoader.GetString("Success_MainWindow_SaveImage_1"), InfoBarInfoType.Auto);
        }
        private async Task CopyImageToClipBoard()
        {
            if (_imageBytes == null || _imageBytes.Length <= 0)
            {
                AppLogger.LogErrorWithInfoBar(AppResourceLoader.GetString("Error_MainWindow_CopyImageToClipBoard_1"));
                return;
            }
            var ms = new InMemoryRandomAccessStream();
            using (DataWriter writer = new DataWriter(ms.GetOutputStreamAt(0)))
            {
                writer.WriteBytes(_imageBytes);
                await writer.StoreAsync();
            }
            var streamReference = RandomAccessStreamReference.CreateFromStream(ms);
            var dataPackage = new DataPackage();
            dataPackage.SetBitmap(streamReference);
            Clipboard.SetContent(dataPackage);
            Clipboard.Flush();
            ms.Dispose();
            AddErrorAndWarningMessageToQueue(InfoBarSeverity.Success, AppResourceLoader.GetString("Success_MainWindow_CopyImageToClipBoard_1"), InfoBarInfoType.Auto);
        }

        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
        [DllImport("user32.dll")]
        static extern bool IsZoomed(IntPtr hWnd);


        private void WindowControlCloseEllipse_PointerReleased(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            _isWindowClosing = true;
            this.Close();
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
            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
            }
            await Launcher.LaunchUriAsync(new Uri(path));
        }

        private void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            string query = SearchTagsAutoSuggestBox.Text;
            ProcessTagsInputAndGetImage(query);
        }

        private async void SaveImageButton_Click(object sender, RoutedEventArgs e)
        {
            SaveImage();
        }

        private async void CopyImageMenuFlyoutItem_Click(object sender, RoutedEventArgs e)
        {
            await CopyImageToClipBoard();
        }

        private void UploadImageButton_Click(object sender, RoutedEventArgs e)
        {
            if (_isUploadImagePageOpen) return;
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
            UploadImagePageGrid.Visibility = Visibility.Collapsed;
            if(_isGettingImage)
            {
                ImageLoadingProgressRing.Visibility = Visibility.Visible;
            }
            else
            {
                DisplayImageGrid.Visibility = Visibility.Visible;
            }
            _uploadImagePage = null;
            _isUploadImagePageOpen = false;
        }

        private void SearchTagsAutoSuggestBox_QuerySubmitted(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs args)
        {
            string query = sender.Text;
            ProcessTagsInputAndGetImage(query);
        }
    }
}
