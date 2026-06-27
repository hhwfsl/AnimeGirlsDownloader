using System;
using System.IO;

namespace AnimeGirlsDownloader
{
    public static class AppConsts
    {
        public static readonly string AppBaseDirectory = AppDomain.CurrentDomain.BaseDirectory;
        public static readonly string ConfigFilePath = Path.Combine(AppBaseDirectory, "config.json");
        public static readonly string AppAssetsDirectory = Path.Combine(AppBaseDirectory, "Assets");
        public static readonly string AnimeGirlBaseEndpoint = "https://kafuumiaki.top/";
        public static readonly string AnimeGirlApiEndpoint = "https://kafuumiaki.top/api/";
        public static readonly string AppUserAgent = "MiakiAnimeGirllDownloader";
        public static readonly string AppLogPath = Path.Combine(AppBaseDirectory, "logs");
        public static readonly string AuthFilePath = Path.Combine(AppBaseDirectory, "auth.token");

    }
}
