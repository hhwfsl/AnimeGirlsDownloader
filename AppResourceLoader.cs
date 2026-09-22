
using Microsoft.Windows.ApplicationModel.Resources;

namespace AnimeGirlsDownloader
{
    public static class AppResourceLoader
    {
        private static readonly ResourceLoader _loader = new();

        public static string GetString(string resourceKey) => _loader.GetString(resourceKey);
    }
}
