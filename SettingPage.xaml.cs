using AnimeGirlsDownloader.Enums;
using AnimeGirlsDownloader.Interfaces;
using AnimeGirlsDownloader.Models;
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
        private Settings _settings = new Settings();
        private Dictionary<ImageType, bool?> _imageTypeToBool = new Dictionary<ImageType, bool?> 
        { { ImageType.SFW, false }, { ImageType.NSFW, true }, { ImageType.ALL, null } };
        public SettingPage()
        {
            InitializeComponent();
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
            _settings = App.Current.Services.GetService<ISettingService>()!.GetSettings();

            IsEnableNSFWCheckBox.IsChecked = _imageTypeToBool[_settings.ImageType];
            if(IsEnableNSFWCheckBox.IsChecked != null)
            {
                IsEnableNSFWCheckBox.Content = AppResourceLoader.GetString((bool)IsEnableNSFWCheckBox.IsChecked? "IsEnableNSFW_CheckBox_Checked": "IsEnableNSFW_CheckBox_Unchecked");
            }
            else
            {
                IsEnableNSFWCheckBox.Content = AppResourceLoader.GetString("IsEnableNSFW_CheckBox_Indeterminate");
            }
            IsEnableFixedSavingPathCheckBox.IsChecked = _settings.IsEnableFixedSavingPath;
            if (_settings.IsEnableFixedSavingPath)
            {
                PathSelectStackPanel.Visibility = Microsoft.UI.Xaml.Visibility.Visible;
            }
            LanguageListComboBox.SelectedItem = LanguageMap.FullToDisplay(_settings.Language);

            if (!File.Exists(_settings.UserAvatarPath))
            {
                _settings.UserAvatarPath = Path.Combine(AppConsts.AppAssetsDirectory, "avatar.png");
                App.Current.Services.GetService<ISettingService>()!
                    .SetUserAvatarPath(_settings.UserAvatarPath)
                    .SaveSetting();
            }
            UserAvatarImageBrush.ImageSource = new BitmapImage(new Uri(_settings.UserAvatarPath));
            if (string.IsNullOrEmpty(_settings.UserName))
            {
                _settings.UserName = App.Current.Services.GetService<ISettingService>()!.SetDefaultUserNameWithSaving();
            }
            UserNameTextBlock.Text = _settings.UserName;
        }
        private void InitializeLogin()
        {
            LoggedStackPanel.Visibility = Microsoft.UI.Xaml.Visibility.Collapsed;
            string? loggedUserName = _settings.LoggedUserName;
            string? loggedUserPassword = _settings.LoggedUserPassword;
            if(string.IsNullOrEmpty(loggedUserName) || string.IsNullOrEmpty(loggedUserPassword))
            {
                return;
            }
            LoggedAccountNameTextBlock.Text = loggedUserName;
            LoggedStackPanel.Visibility = Microsoft.UI.Xaml.Visibility.Visible;
            LoginButton.Visibility = Microsoft.UI.Xaml.Visibility.Collapsed;
        }
        private void AfterLoginHandler(string userName, string password)
        {
            LoggedAccountNameTextBlock.Text = userName;
            _settings.LoggedUserName = userName;
            _settings.LoggedUserPassword = password;
            App.Current.Services.GetService<ISettingService>()!.SaveSetting(_settings);
            InitializeLogin();
        }

        private void IsEnableFixedSavingPathCheckBox_Checked(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            PathSelectStackPanel.Visibility = Microsoft.UI.Xaml.Visibility.Visible;
            _settings.IsEnableFixedSavingPath = true;
            App.Current.Services.GetService<ISettingService>()!.SaveSetting(_settings);
        }

        private void IsEnableFixedSavingPathCheckBox_Unchecked(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            PathSelectStackPanel.Visibility = Microsoft.UI.Xaml.Visibility.Collapsed;
            _settings.IsEnableFixedSavingPath = false;
            App.Current.Services.GetService<ISettingService>()!.SaveSetting(_settings);
        }

        private async void PathSelectButton_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            var folderPicker = App.Current.Services.GetService<IFileService>();
            if (folderPicker != null)
            {
                StorageFolder? folder = await folderPicker.PickFolderAsync();
                if (folder != null)
                {
                    _settings.SavingPath = folder.Path;
                    App.Current.Services.GetService<ISettingService>()!.SaveSetting(_settings);
                }
            }
        }

        private void IsEnableNSFWCheckBox_Checked(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            _settings.ImageType = ImageType.NSFW;
            App.Current.Services.GetService<ISettingService>()!.SaveSetting(_settings);
            IsEnableNSFWCheckBox.Content = AppResourceLoader.GetString("IsEnableNSFW_CheckBox_Checked");
        }

        private void IsEnableNSFWCheckBox_Unchecked(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            _settings.ImageType = ImageType.SFW;
            App.Current.Services.GetService<ISettingService>()!.SaveSetting(_settings);
            IsEnableNSFWCheckBox.Content = AppResourceLoader.GetString("IsEnableNSFW_CheckBox_Unchecked");
        }
        private void IsEnableNSFWCheckBox_Indeterminate(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            _settings.ImageType = ImageType.ALL;
            App.Current.Services.GetService<ISettingService>()!.SaveSetting(_settings);
            IsEnableNSFWCheckBox.Content = AppResourceLoader.GetString("IsEnableNSFW_CheckBox_Indeterminate");
        }

        private void ResizeWindowButton_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            App.Current.Services.GetService<ISettingService>()!.ResizeWindowToStandardSize();
        }

        private void LanguageListComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            string selectedItem = LanguageListComboBox.SelectedItem.ToString() ?? string.Empty;
            if (string.IsNullOrEmpty(selectedItem))
            {
                AppLogger.LogError(AppResourceLoader.GetString("Error_SettingPage_LanguageListComboBoxSelectionChanged_1"));
                return;
            }
            string languageCode = LanguageMap.SimpleToFull(LanguageMap.DisplayToSimple(selectedItem));
            App.Current.Services.GetService<ISettingService>()!.SetLanguage(languageCode).SaveSetting();
            _settings.Language = languageCode;

        }

        private async void EditUserAvatarMenuFlyoutItem_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            var filePicker = App.Current.Services.GetService<IFileService>();
            if (filePicker != null)
            {
                StorageFile? file = await filePicker.PickImageAsync();
                if (file != null)
                {
                    _settings.UserAvatarPath = file.Path;
                    UserAvatarImageBrush.ImageSource = new BitmapImage(new Uri(file.Path));
                    App.Current.Services.GetService<ISettingService>()!.SaveSetting(_settings);
                }
            }
        }

        private void EditUserNameMenuFlyoutItem_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            string oldUserName = UserNameTextBlock.Text.Trim();
            UserNameTextBox.Text = oldUserName;
            UserNameTextBlock.Visibility = Microsoft.UI.Xaml.Visibility.Collapsed;
            UserNameTextBox.Visibility = Microsoft.UI.Xaml.Visibility.Visible;
            UserNameTextBox.Focus(Microsoft.UI.Xaml.FocusState.Programmatic);
        }
        private void EditUserName()
        {
            string newUserName = UserNameTextBox.Text.Trim();
            if (string.IsNullOrEmpty(newUserName))
            {
                UserNameTextBox.Text = _settings.UserName;
                //AppLogger.LogWarning(AppResourceLoader.GetString("Warning_SettingPage_UserNameTextBoxLostFocus_1"));
                UserNameTextBlock.Visibility = Microsoft.UI.Xaml.Visibility.Visible;
                UserNameTextBox.Visibility = Microsoft.UI.Xaml.Visibility.Collapsed;
                return;
            }
            UserNameTextBlock.Text = newUserName;
            _settings.UserName = newUserName;
            App.Current.Services.GetService<ISettingService>()!.SetUserName(newUserName).SaveSetting();
            UserNameTextBlock.Visibility = Microsoft.UI.Xaml.Visibility.Visible;
            UserNameTextBox.Visibility = Microsoft.UI.Xaml.Visibility.Collapsed;
        }
        private void UserNameTextBox_LostFocus(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            EditUserName();
        }

        private void UserNameTextBox_KeyDown(object sender, Microsoft.UI.Xaml.Input.KeyRoutedEventArgs e)
        {
            if (e.Key == Windows.System.VirtualKey.Enter)
            {
                EditUserName();
            }
        }

        private async void LoginButton_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
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
            _settings.LoggedUserName = null;
            _settings.LoggedUserPassword = null;
            App.Current.Services.GetService<ISettingService>()!.SaveSetting(_settings);
            if(File.Exists(AppConsts.AuthFilePath))
            {
                File.Delete(AppConsts.AuthFilePath);
            }
            LoggedStackPanel.Visibility = Microsoft.UI.Xaml.Visibility.Collapsed;
            LoginButton.Visibility = Microsoft.UI.Xaml.Visibility.Visible;
        }

        
    }
}
