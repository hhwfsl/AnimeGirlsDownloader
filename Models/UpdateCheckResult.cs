using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace AnimeGirlsDownloader.Models;

public enum UpdateCheckStatus
{
    UpToDate,
    UpdateAvailable,
    Failed,
}

public sealed class UpdateCheckResult
{
    public required UpdateCheckStatus Status { get; init; }
    public required string CurrentVersion { get; init; }
    public string? LatestVersion { get; init; }
    public Uri? DownloadUri { get; init; }
    public string? ErrorMessage { get; init; }
}

internal sealed class GitHubReleaseResponse
{
    [JsonPropertyName("tag_name")]
    public required string TagName { get; init; }

    [JsonPropertyName("html_url")]
    public required string HtmlUrl { get; init; }

    [JsonPropertyName("assets")]
    public List<GitHubReleaseAsset> Assets { get; init; } = [];
}

internal sealed class GitHubReleaseAsset
{
    [JsonPropertyName("name")]
    public required string Name { get; init; }

    [JsonPropertyName("browser_download_url")]
    public required string BrowserDownloadUrl { get; init; }
}
