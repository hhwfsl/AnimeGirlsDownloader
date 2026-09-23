using AnimeGirlsDownloader.Enums;
using AnimeGirlsDownloader.Interfaces;
using AnimeGirlsDownloader.Models;
using AnimeGirlsDownloader.Responses;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media.Imaging;
using System;
using System.Collections.Generic;
using System.IO;
using Windows.Storage;


namespace AnimeGirlsDownloader
{
    public sealed partial class SettingPage : Page
    {
        private Settings _settings = new();
        private readonly Dictionary<ImageType, bool?> _imageTypeToBool = new()
        { { ImageType.SFW, false }, { ImageType.NSFW, true }, { ImageType.ALL, null } };
        
        private readonly Dictionary<ImageIsAllowAiType, bool?> _imageIsAllowAiTypeToBool = new()
        { { ImageIsAllowAiType.NotAllowAi, false }, { ImageIsAllowAiType.AiOnly, null }, { ImageIsAllowAiType.ALL, true } };

        private readonly ISettingService _settingService;
        private readonly IFileService _fileService;
        private readonly IUserSessionService _userSessionService;
        private readonly IUpdateService _updateService;
        private bool _isSavingUserName;
        public SettingPage()
        {
            InitializeComponent();
            _settingService = App.Current.Services.GetRequiredService<ISettingService>();
            _fileService = App.Current.Services.GetRequiredService<IFileService>();
            _userSessionService = App.Current.Services.GetRequiredService<IUserSessionService>();
            _updateService = App.Current.Services.GetRequiredService<IUpdateService>();
            CurrentVersionValueTextBlock.Text = _updateService.CurrentVersion;
            _userSessionService.UserChanged += UserSessionService_UserChanged;
            Unloaded += SettingPage_Unloaded;
            Initialize();
        }
        private void Initialize()
        {
            PathSelectStackPanel.Visibility = Microsoft.UI.Xaml.Visibility.Collapsed;
            
            InitializeSettings();
            InitializeLogin();
        }
        private void InitializeSettings()
        {
            _settings = _settingService.GetSettings();

            IsEnableNSFWCheckBox.IsChecked = _imageTypeToBool[_settings.ImageType];
            IsEnableAiGeneratedCheckBox.IsChecked = _imageIsAllowAiTypeToBool[_settings.IsAllowAiGenerated];
            if(IsEnableNSFWCheckBox.IsChecked != null)
            {
                IsEnableNSFWCheckBox.Content = AppResourceLoader.GetString((bool)IsEnableNSFWCheckBox.IsChecked? "IsEnableNSFW_CheckBox_Checked": "IsEnableNSFW_CheckBox_Unchecked");
            }
            else
            {
                IsEnableNSFWCheckBox.Content = AppResourceLoader.GetString("IsEnableNSFW_CheckBox_Indeterminate");
            }
            if(IsEnableAiGeneratedCheckBox.IsChecked != null)
            {
                IsEnableAiGeneratedCheckBox.Content = AppResourceLoader.GetString((bool)IsEnableAiGeneratedCheckBox.IsChecked ? "IsEnableAiGenerated_CheckBox_Checked" : "IsEnableAiGenerated_CheckBox_Unchecked");
            }
            else
            {
                IsEnableAiGeneratedCheckBox.Content = AppResourceLoader.GetString("IsEnableAiGenerated_CheckBox_Indeterminate");
            }
            IsEnableFixedSavingPathCheckBox.IsChecked = _settings.IsEnableFixedSavingPath;
            if (_settings.IsEnableFixedSavingPath)
            {
                PathSelectStackPanel.Visibility = Microsoft.UI.Xaml.Visibility.Visible;
            }
            LanguageListComboBox.SelectedItem = LanguageMap.FullToDisplay(_settings.Language);

            if (!File.Exists(_settings.UserAvatarPath))
            {
                _settings.UserAvatarPath = Path.Combine(AppPaths.AssetsDirectory, "avatar.png");
                _settingService.SetUserAvatarPath(_settings.UserAvatarPath).SaveSetting();
            }
            _ = LoadAvatarImageAsync(_settings.UserAvatarPath);
            if (string.IsNullOrEmpty(_settings.UserName))
            {
                _settings.UserName = _settingService.SetDefaultUserNameWithSaving();
            }
            UserNameTextBlock.Text = _settings.UserName;
        }
        private void InitializeLogin()
        {
            LoggedStackPanel.Visibility = Microsoft.UI.Xaml.Visibility.Collapsed;
            LogoutButton.Visibility = Microsoft.UI.Xaml.Visibility.Collapsed;
            LoginButton.Visibility = Microsoft.UI.Xaml.Visibility.Visible;
            string? loggedUserName = _settings.LoggedUserName;
            if(string.IsNullOrEmpty(loggedUserName))
            {
                return;
            }
            LoggedAccountNameTextBlock.Text = loggedUserName;
            LoggedStackPanel.Visibility = Microsoft.UI.Xaml.Visibility.Visible;
            LoginButton.Visibility = Microsoft.UI.Xaml.Visibility.Collapsed;
            LogoutButton.Visibility = Microsoft.UI.Xaml.Visibility.Visible;
        }
        private void AfterLoginHandler(UserProfileResponse profile)
        {
            _settings = _settingService.GetSettings();
            LoggedAccountNameTextBlock.Text = profile.Name;
            InitializeSettings();
            InitializeLogin();
        }

        private void UserSessionService_UserChanged(object? sender, UserProfileResponse? profile)
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                _settings = _settingService.GetSettings();
                InitializeSettings();
                InitializeLogin();
            });
        }

        private void SettingPage_Unloaded(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            _userSessionService.UserChanged -= UserSessionService_UserChanged;
        }

        private void IsEnableFixedSavingPathCheckBox_Checked(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            PathSelectStackPanel.Visibility = Microsoft.UI.Xaml.Visibility.Visible;
            _settings.IsEnableFixedSavingPath = true;
            _settingService.SaveSetting(_settings);
        }

        private void IsEnableFixedSavingPathCheckBox_Unchecked(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            PathSelectStackPanel.Visibility = Microsoft.UI.Xaml.Visibility.Collapsed;
            _settings.IsEnableFixedSavingPath = false;
            _settingService.SaveSetting(_settings);
        }

        private async void PathSelectButton_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            try
            {
                StorageFolder? folder = await _fileService.PickFolderAsync();
                if (folder != null)
                {
                    _settings.SavingPath = folder.Path;
                    _settingService.SaveSetting(_settings);
                }
            }
            catch (Exception exception)
            {
                AppLogger.LogErrorWithInfoBar(exception.Message);
            }
        }

        private void IsEnableNSFWCheckBox_Checked(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            _settings.ImageType = ImageType.NSFW;
            _settingService.SaveSetting(_settings);
            IsEnableNSFWCheckBox.Content = AppResourceLoader.GetString("IsEnableNSFW_CheckBox_Checked");
        }

        private void IsEnableNSFWCheckBox_Unchecked(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            _settings.ImageType = ImageType.SFW;
            _settingService.SaveSetting(_settings);
            IsEnableNSFWCheckBox.Content = AppResourceLoader.GetString("IsEnableNSFW_CheckBox_Unchecked");
        }
        private void IsEnableNSFWCheckBox_Indeterminate(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            _settings.ImageType = ImageType.ALL;
            _settingService.SaveSetting(_settings);
            IsEnableNSFWCheckBox.Content = AppResourceLoader.GetString("IsEnableNSFW_CheckBox_Indeterminate");
        }

        private void ResizeWindowButton_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            _settingService.ResizeWindowToStandardSize();
        }

        private void LanguageListComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            string selectedItem = LanguageListComboBox.SelectedItem?.ToString() ?? string.Empty;
            if (string.IsNullOrEmpty(selectedItem))
            {
                AppLogger.LogErrorWithInfoBar(AppResourceLoader.GetString("Error_SettingPage_LanguageListComboBoxSelectionChanged_1"));
                return;
            }
            string languageCode = LanguageMap.SimpleToFull(LanguageMap.DisplayToSimple(selectedItem));
            _settingService.SetLanguage(languageCode).SaveSetting();
            _settings.Language = languageCode;
        }

        private async void CheckForUpdatesButton_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            CheckForUpdatesButton.IsEnabled = false;
            try
            {
                UpdateCheckResult result = await _updateService.CheckForUpdateAsync();
                if (result.Status == UpdateCheckStatus.UpdateAvailable && XamlRoot is not null)
                {
                    await UpdatePrompt.ShowAsync(XamlRoot, result);
                }
                else if (result.Status == UpdateCheckStatus.UpToDate)
                {
                    AppLogger.LogSuccessWithInfoBar(
                        AppResourceLoader.GetString("Success_UpdateService_UpToDate_1"),
                        InfoBarInfoType.Auto);
                }
                else
                {
                    AppLogger.LogWarningWithInfoBar(
                        AppResourceLoader.GetString("Warning_UpdateService_CheckFailed_1"),
                        InfoBarInfoType.Auto);
                }
            }
            finally
            {
                CheckForUpdatesButton.IsEnabled = true;
            }
        }

        private async void EditUserAvatarMenuFlyoutItem_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            if (_userSessionService.CurrentUser is null)
            {
                AppLogger.LogWarningWithInfoBar(
                    AppResourceLoader.GetString("Warning_Profile_LoginRequired_1"),
                    InfoBarInfoType.Auto);
                return;
            }

            EditUserAvatarMenuFlyoutItem.IsEnabled = false;
            try
            {
                StorageFile? file = await _fileService.PickImageAsync();
                if (file is null || XamlRoot is null) return;

                using var cropDialog = new AvatarCropDialog(file.Path) { XamlRoot = XamlRoot };
                if (await cropDialog.ShowAsync() != ContentDialogResult.Primary) return;

                await _userSessionService.UpdateAvatarAsync(cropDialog.GetCroppedPng());
                _settings = _settingService.GetSettings();
                await LoadAvatarImageAsync(_settings.UserAvatarPath);
                AppLogger.LogSuccessWithInfoBar(
                    AppResourceLoader.GetString("Success_Profile_Updated_1"),
                    InfoBarInfoType.Auto);
            }
            catch (Exception exception)
            {
                AppLogger.LogErrorWithInfoBar(
                    $"{AppResourceLoader.GetString("Error_Profile_UpdateFailed_1")} {exception.Message}");
            }
            finally
            {
                EditUserAvatarMenuFlyoutItem.IsEnabled = true;
            }
        }

        private void EditUserNameMenuFlyoutItem_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            if (_userSessionService.CurrentUser is null)
            {
                AppLogger.LogWarningWithInfoBar(
                    AppResourceLoader.GetString("Warning_Profile_LoginRequired_1"),
                    InfoBarInfoType.Auto);
                return;
            }

            string oldUserName = UserNameTextBlock.Text.Trim();
            UserNameTextBox.Text = oldUserName;
            UserNameTextBlock.Visibility = Microsoft.UI.Xaml.Visibility.Collapsed;
            UserNameTextBox.Visibility = Microsoft.UI.Xaml.Visibility.Visible;
            UserNameTextBox.Focus(Microsoft.UI.Xaml.FocusState.Programmatic);
        }
        private async System.Threading.Tasks.Task EditUserNameAsync()
        {
            if (_isSavingUserName) return;
            string newUserName = UserNameTextBox.Text.Trim();
            if (string.IsNullOrEmpty(newUserName))
            {
                UserNameTextBox.Text = _settings.UserName;
                //AppLogger.LogWarning(AppResourceLoader.GetString("Warning_SettingPage_UserNameTextBoxLostFocus_1"));
                UserNameTextBlock.Visibility = Microsoft.UI.Xaml.Visibility.Visible;
                UserNameTextBox.Visibility = Microsoft.UI.Xaml.Visibility.Collapsed;
                return;
            }

            if (string.Equals(newUserName, _settings.UserName, StringComparison.Ordinal))
            {
                UserNameTextBlock.Visibility = Microsoft.UI.Xaml.Visibility.Visible;
                UserNameTextBox.Visibility = Microsoft.UI.Xaml.Visibility.Collapsed;
                return;
            }

            _isSavingUserName = true;
            UserNameTextBox.IsEnabled = false;
            try
            {
                UserProfileResponse profile = await _userSessionService.UpdateUserNameAsync(newUserName);
                _settings = _settingService.GetSettings();
                UserNameTextBlock.Text = profile.Name;
                LoggedAccountNameTextBlock.Text = profile.Name;
                AppLogger.LogSuccessWithInfoBar(
                    AppResourceLoader.GetString("Success_Profile_Updated_1"),
                    InfoBarInfoType.Auto);
            }
            catch (Exception exception)
            {
                UserNameTextBox.Text = _settings.UserName;
                AppLogger.LogErrorWithInfoBar(
                    $"{AppResourceLoader.GetString("Error_Profile_UpdateFailed_1")} {exception.Message}");
            }
            finally
            {
                _isSavingUserName = false;
                UserNameTextBox.IsEnabled = true;
                UserNameTextBlock.Visibility = Microsoft.UI.Xaml.Visibility.Visible;
                UserNameTextBox.Visibility = Microsoft.UI.Xaml.Visibility.Collapsed;
            }
        }
        private async void UserNameTextBox_LostFocus(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            await EditUserNameAsync();
        }

        private async void UserNameTextBox_KeyDown(object sender, Microsoft.UI.Xaml.Input.KeyRoutedEventArgs e)
        {
            if (e.Key == Windows.System.VirtualKey.Enter)
            {
                e.Handled = true;
                await EditUserNameAsync();
            }
        }

        private async System.Threading.Tasks.Task LoadAvatarImageAsync(string path)
        {
            try
            {
                StorageFile file = await StorageFile.GetFileFromPathAsync(path);
                using var stream = await file.OpenReadAsync();
                var image = new BitmapImage();
                await image.SetSourceAsync(stream);
                if (string.Equals(path, _settings.UserAvatarPath, StringComparison.OrdinalIgnoreCase))
                    UserAvatarImageBrush.ImageSource = image;
            }
            catch (Exception exception)
            {
                AppLogger.LogWarning($"Unable to display the cached avatar. {exception.Message}");
            }
        }

        private void LoginButton_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            Button button = (Button)sender;
            Frame frame = new Frame();
            LoginPage loginPage = new LoginPage();
            loginPage.Initialize(AfterLoginHandler);
            frame.Content = loginPage;

            Flyout flyout = new Flyout
            {
                Placement = FlyoutPlacementMode.Left,
                Content = frame,
            };
            flyout.ShowAt(button);
        }

        private void LogoutButton_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            _userSessionService.SignOut();
            _settings = _settingService.GetSettings();
            InitializeSettings();
            InitializeLogin();
        }

        private void IsEnableAiGeneratedCheckBox_Checked(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            _settings.IsAllowAiGenerated = ImageIsAllowAiType.ALL;
            _settingService.SaveSetting(_settings);
            IsEnableAiGeneratedCheckBox.Content = AppResourceLoader.GetString("IsEnableAiGenerated_CheckBox_Checked");
        }

        private void IsEnableAiGeneratedCheckBox_Unchecked(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            _settings.IsAllowAiGenerated = ImageIsAllowAiType.NotAllowAi;
            _settingService.SaveSetting(_settings);
            IsEnableAiGeneratedCheckBox.Content = AppResourceLoader.GetString("IsEnableAiGenerated_CheckBox_Unchecked");
        }

        private void IsEnableAiGeneratedCheckBox_Indeterminate(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            _settings.IsAllowAiGenerated = ImageIsAllowAiType.AiOnly;
            _settingService.SaveSetting(_settings);
            IsEnableAiGeneratedCheckBox.Content = AppResourceLoader.GetString("IsEnableAiGenerated_CheckBox_Indeterminate");
        }
    }
}
