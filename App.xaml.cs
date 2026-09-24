using AnimeGirlsDownloader.Interfaces;
using AnimeGirlsDownloader.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.Windows.Globalization;
using System;
using System.Net.Http;

namespace AnimeGirlsDownloader;

public partial class App : Application
{
    private Window? _window;

    public App()
    {
        AppPaths.EnsureDataDirectories();

        var settingService = new SettingService();
        InitializeComponent();
        InitializeLanguage(settingService);

        var httpClient = new HttpClient
        {
            BaseAddress = AppConsts.AnimeGirlsApiEndpoint,
            Timeout = TimeSpan.FromSeconds(60),
        };
        httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(AppConsts.AppUserAgent);

        var services = new ServiceCollection();
        services.AddSingleton<ISettingService>(settingService);
        services.AddSingleton<IFileService, FileService>();
        services.AddSingleton<ITokenStore, TokenStore>();
        services.AddSingleton(httpClient);
        services.AddSingleton<IAnimeGirlsApiClient, AnimeGirlsApiClient>();
        services.AddSingleton<IUserSessionService, UserSessionService>();
        services.AddSingleton<ISelfUpdateService, SelfUpdateService>();
        services.AddSingleton<IDownloadManager, DownloadManager>();
        services.AddSingleton<IUpdateService, UpdateService>();
        Services = services.BuildServiceProvider();
    }

    public static new App Current => (App)Application.Current;

    public IServiceProvider Services { get; }

    private static void InitializeLanguage(ISettingService settingService) =>
        ApplicationLanguages.PrimaryLanguageOverride = settingService.GetSettings().Language;

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        _window = new MainWindow();
        _window.Activate();
    }

    public void ExitForUpdate()
    {
        if (_window is null)
        {
            Environment.Exit(0);
            return;
        }

        if (!_window.DispatcherQueue.TryEnqueue(() =>
        {
            _window.Close();
            Environment.Exit(0);
        }))
        {
            Environment.Exit(0);
        }
    }
}
