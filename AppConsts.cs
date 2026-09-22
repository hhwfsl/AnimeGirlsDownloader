using System;

namespace AnimeGirlsDownloader;

public static class AppConsts
{
    public static readonly Uri AnimeGirlsApiEndpoint = new("https://kafuumiaki.top/api/", UriKind.Absolute);
    public const string AnimeGirlsBaseEndpoint = "https://kafuumiaki.top/";
    public const string AnimeGirlsImageIdLinkEndpoint = "https://kafuumiaki.top/api/Image/images/";
    public static readonly Uri GitHubLatestReleaseApiEndpoint = new(
        "https://api.github.com/repos/hhwfsl/AnimeGirlsDownloader/releases/latest",
        UriKind.Absolute);
    public const string AppUserAgent = "AnimeGirlsDownloader/1.0";
}
