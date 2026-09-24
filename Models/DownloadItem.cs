using AnimeGirlsDownloader.Enums;
using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace AnimeGirlsDownloader.Models;

public sealed class DownloadItem : INotifyPropertyChanged
{
    private double _progress;
    private DownloadStatus _status = DownloadStatus.Queued;
    private string? _errorMessage;
    private string _destinationPath = string.Empty;

    public Guid Id { get; } = Guid.NewGuid();
    public DownloadItemKind Kind { get; set; } = DownloadItemKind.Image;
    public long UserId { get; set; }
    public string ImageId { get; set; } = string.Empty;
    public string DownloadUrl { get; set; } = string.Empty;
    public string DestinationDirectory { get; set; } = string.Empty;
    public string? Version { get; set; }
    public long ExpectedSize { get; set; } = -1;
    public string? ExpectedDigest { get; set; }
    public bool IsGlobal => Kind == DownloadItemKind.ApplicationUpdate;
    public string DisplayName => Kind == DownloadItemKind.ApplicationUpdate
        ? string.Format(AppResourceLoader.GetString("DownloadItem_UpdateDisplayName"), Version)
        : string.Format(AppResourceLoader.GetString("DownloadItem_DisplayName"), ImageId);

    public double Progress { get => _progress; set => SetField(ref _progress, value); }
    public DownloadStatus Status
    {
        get => _status;
        set
        {
            if (!SetField(ref _status, value)) return;
            OnPropertyChanged(nameof(StatusText));
            OnPropertyChanged(nameof(CanCancel));
            OnPropertyChanged(nameof(CanRetry));
        }
    }
    public string StatusText => AppResourceLoader.GetString($"DownloadStatus_{Status}");
    public bool CanCancel => Status is DownloadStatus.Queued or DownloadStatus.Downloading;
    public bool CanRetry => Status is DownloadStatus.Failed;
    public string? ErrorMessage
    {
        get => _errorMessage;
        set
        {
            if (SetField(ref _errorMessage, value))
                OnPropertyChanged(nameof(ProgressToolTip));
        }
    }
    public string DestinationPath
    {
        get => _destinationPath;
        set
        {
            if (SetField(ref _destinationPath, value))
                OnPropertyChanged(nameof(ProgressToolTip));
        }
    }
    public string ProgressToolTip => string.IsNullOrWhiteSpace(ErrorMessage)
        ? DestinationPath
        : $"{DestinationPath}{Environment.NewLine}{ErrorMessage}";

    public event PropertyChangedEventHandler? PropertyChanged;

    private bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
