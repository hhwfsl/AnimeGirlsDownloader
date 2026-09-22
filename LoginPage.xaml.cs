using AnimeGirlsDownloader.Interfaces;
using AnimeGirlsDownloader.Services;
using AnimeGirlsDownloader.Responses;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Threading.Tasks;

namespace AnimeGirlsDownloader;

public sealed partial class LoginPage : Page
{
    private readonly IAnimeGirlsApiClient _apiClient;
    private readonly IUserSessionService _userSessionService;
    private Action<UserProfileResponse>? _afterLogin;

    public LoginPage()
    {
            InitializeComponent();
            _apiClient = App.Current.Services.GetRequiredService<IAnimeGirlsApiClient>();
            _userSessionService = App.Current.Services.GetRequiredService<IUserSessionService>();
        }

    public void Initialize(Action<UserProfileResponse> afterLogin)
    {
        _afterLogin = afterLogin ?? throw new ArgumentNullException(nameof(afterLogin));
    }

    private async void LoginPageLoginButton_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        await AuthenticateAsync(isRegistration: false);
    }

    private async void LoginPageRegistButton_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        await AuthenticateAsync(isRegistration: true);
    }

    private async Task AuthenticateAsync(bool isRegistration)
    {
        string userName = LoginAccountTextBox.Text.Trim();
        string password = LoginPasswordTextBox.Password;
        if (string.IsNullOrWhiteSpace(userName) || string.IsNullOrWhiteSpace(password))
        {
            ShowTip(
                AppResourceLoader.GetString("LoginPageTip_TeachingTip_Title"),
                AppResourceLoader.GetString("LoginPageTip_TeachingTip_Subtitle"));
            return;
        }

        LoginPageLoginButton.IsEnabled = false;
        LoginPageRegistButton.IsEnabled = false;
        try
        {
            bool succeeded = isRegistration
                ? await _apiClient.RegisterAsync(userName, password)
                : await _apiClient.LoginAsync(userName, password);
            if (!succeeded)
            {
                return;
            }

            UserProfileResponse profile = await _userSessionService.SynchronizeAsync();
            _afterLogin?.Invoke(profile);
            ShowTip(
                AppResourceLoader.GetString("LoginPageSuccess_TeachingTip_Title"),
                AppResourceLoader.GetString(isRegistration
                    ? "LoginPageSuccess_TeachingTip_Subtitle_Register"
                    : "LoginPageSuccess_TeachingTip_Subtitle_Login"));
        }
        catch (ApiClientException exception)
        {
            AppLogger.LogError(exception.Message);
            ShowTip(
                AppResourceLoader.GetString("LoginPageError_TeachingTip_Title"),
                exception.Message);
        }
        finally
        {
            LoginPageLoginButton.IsEnabled = true;
            LoginPageRegistButton.IsEnabled = true;
        }
    }

    private void ShowTip(string title, string subtitle)
    {
        LoginPageTipTeachingTip.Title = title;
        LoginPageTipTeachingTip.Subtitle = subtitle;
        LoginPageTipTeachingTip.IsOpen = true;
    }

    private async void LoginInput_KeyDown(object sender, Microsoft.UI.Xaml.Input.KeyRoutedEventArgs e)
    {
        if (e.Key != Windows.System.VirtualKey.Enter || !LoginPageLoginButton.IsEnabled)
            return;
        e.Handled = true;
        await AuthenticateAsync(isRegistration: false);
    }
}
