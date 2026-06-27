using AnimeGirlsDownloader.Enums;
using Microsoft.UI.Xaml.Controls;

namespace AnimeGirlsDownloader;

public class InfoMessage
{
    public string Message { get; set; } = string.Empty;
    public InfoBarInfoType InfoBarType { get; set; }
    public InfoBarSeverity Severity { get; set; }
}
