using AnimeGirlsDownloader.Enums;
using AnimeGirlsDownloader.Models;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Threading;
using System.Threading.Tasks;
using Windows.System;

namespace AnimeGirlsDownloader;

public static class UpdatePrompt
{
    private static readonly SemaphoreSlim DialogLock = new(1, 1);

    public static async Task ShowAsync(XamlRoot xamlRoot, UpdateCheckResult update)
    {
        if (update.Status != UpdateCheckStatus.UpdateAvailable || update.DownloadUri is null) return;
        await DialogLock.WaitAsync();
        try
        {
            var dialog = new ContentDialog
            {
                XamlRoot = xamlRoot,
                Title = AppResourceLoader.GetString("UpdateAvailable_Dialog_Title"),
                Content = string.Format(
                    AppResourceLoader.GetString("UpdateAvailable_Dialog_Message"),
                    update.CurrentVersion,
                    update.LatestVersion),
                PrimaryButtonText = AppResourceLoader.GetString("UpdateAvailable_Dialog_Download"),
                CloseButtonText = AppResourceLoader.GetString("UpdateAvailable_Dialog_Cancel"),
                DefaultButton = ContentDialogButton.Primary,
            };
            if (await dialog.ShowAsync() != ContentDialogResult.Primary) return;

            bool launched = await Launcher.LaunchUriAsync(update.DownloadUri);
            if (!launched)
            {
                AppLogger.LogWarningWithInfoBar(
                    AppResourceLoader.GetString("Error_UpdatePrompt_OpenDownload_1"),
                    InfoBarInfoType.Auto);
            }
        }
        catch (Exception exception)
        {
            AppLogger.LogWarning(exception.Message);
        }
        finally
        {
            DialogLock.Release();
        }
    }
}
