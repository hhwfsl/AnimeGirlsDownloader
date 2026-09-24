using AnimeGirlsDownloader.Enums;
using AnimeGirlsDownloader.Interfaces;
using AnimeGirlsDownloader.Models;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Threading;
using System.Threading.Tasks;

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

            IDownloadManager downloadManager = App.Current.Services.GetRequiredService<IDownloadManager>();
            DownloadItem item = downloadManager.EnqueueUpdate(update);
            AppLogger.LogInfoWithInfoBar(
                string.Format(AppResourceLoader.GetString("Info_UpdatePrompt_UpdateQueued_1"), item.DisplayName),
                InfoBarInfoType.Auto);
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
