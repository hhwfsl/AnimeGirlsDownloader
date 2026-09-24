using AnimeGirlsDownloader.Interfaces;
using AnimeGirlsDownloader.Models;
using AnimeGirlsDownloader.Serialization;
using System;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace AnimeGirlsDownloader.Services;

public sealed class UpdateService : IUpdateService
{
    private readonly HttpClient _httpClient;
    private readonly SemaphoreSlim _checkLock = new(1, 1);

    public UpdateService(HttpClient httpClient)
    {
        _httpClient = httpClient;
        Version version = typeof(App).Assembly.GetName().Version ?? new Version(1, 0, 0);
        CurrentVersion = FormatVersion(version);
    }

    public string CurrentVersion { get; }

    public async Task<UpdateCheckResult> CheckForUpdateAsync(CancellationToken cancellationToken = default)
    {
        await _checkLock.WaitAsync(cancellationToken);
        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(10));
            using var request = new HttpRequestMessage(HttpMethod.Get, AppConsts.GitHubLatestReleaseApiEndpoint);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
            request.Headers.TryAddWithoutValidation("X-GitHub-Api-Version", "2022-11-28");

            using HttpResponseMessage response = await _httpClient.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                timeout.Token);
            if (!response.IsSuccessStatusCode)
                return Failed($"GitHub returned {(int)response.StatusCode} ({response.ReasonPhrase}).");

            await using System.IO.Stream stream = await response.Content.ReadAsStreamAsync(timeout.Token);
            GitHubReleaseResponse? release = await JsonSerializer.DeserializeAsync(
                stream,
                AppJsonSerializerContext.Default.GitHubReleaseResponse,
                timeout.Token);
            if (release is null || !TryParseVersion(release.TagName, out Version? latestVersion))
                return Failed("The latest GitHub release has an invalid version tag.");

            Version currentVersion = ParseCurrentVersion();
            if (latestVersion <= currentVersion)
            {
                return new UpdateCheckResult
                {
                    Status = UpdateCheckStatus.UpToDate,
                    CurrentVersion = CurrentVersion,
                    LatestVersion = FormatVersion(latestVersion),
                };
            }

            GitHubReleaseAsset? asset = SelectDownloadAsset(release);
            return asset is null || !Uri.TryCreate(asset.BrowserDownloadUrl, UriKind.Absolute, out Uri? downloadUri)
                ? Failed("The latest GitHub release does not contain a compatible Windows ZIP package.")
                : new UpdateCheckResult
                {
                    Status = UpdateCheckStatus.UpdateAvailable,
                    CurrentVersion = CurrentVersion,
                    LatestVersion = FormatVersion(latestVersion),
                    DownloadUri = downloadUri,
                    AssetName = asset.Name,
                    AssetSize = asset.Size,
                    AssetDigest = asset.Digest,
                };
        }
        catch (Exception exception) when (exception is HttpRequestException or JsonException or TaskCanceledException)
        {
            return Failed(exception.Message);
        }
        finally
        {
            _checkLock.Release();
        }
    }

    private UpdateCheckResult Failed(string message) => new()
    {
        Status = UpdateCheckStatus.Failed,
        CurrentVersion = CurrentVersion,
        ErrorMessage = message,
    };

    private Version ParseCurrentVersion() =>
        Version.TryParse(CurrentVersion, out Version? version) ? version : new Version(1, 0, 0);

    private static bool TryParseVersion(string tag, out Version version)
    {
        string value = tag.Trim().TrimStart('v', 'V');
        int suffixIndex = value.IndexOfAny(['-', '+']);
        if (suffixIndex >= 0) value = value[..suffixIndex];
        if (Version.TryParse(value, out Version? parsedVersion))
        {
            version = parsedVersion;
            return true;
        }
        version = new Version(0, 0);
        return false;
    }

    private static string FormatVersion(Version version) =>
        version.Build >= 0
            ? $"{version.Major}.{version.Minor}.{version.Build}"
            : $"{version.Major}.{version.Minor}";

    private static GitHubReleaseAsset? SelectDownloadAsset(GitHubReleaseResponse release)
    {
        return release.Assets
            .Where(IsTrustedZipAsset)
            .OrderByDescending(asset => GetAssetScore(asset.Name))
            .FirstOrDefault();
    }

    private static bool IsTrustedZipAsset(GitHubReleaseAsset asset) =>
        asset.Name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase) &&
        Uri.TryCreate(asset.BrowserDownloadUrl, UriKind.Absolute, out Uri? uri) &&
        uri.Scheme == Uri.UriSchemeHttps &&
        uri.Host.Equals("github.com", StringComparison.OrdinalIgnoreCase) &&
        uri.AbsolutePath.StartsWith(
            "/hhwfsl/AnimeGirlsDownloader/releases/download/",
            StringComparison.OrdinalIgnoreCase);

    private static int GetAssetScore(string name)
    {
        string normalized = name.ToLowerInvariant();
        int score = 0;
        string architecture = RuntimeInformation.ProcessArchitecture switch
        {
            Architecture.X64 => "x64",
            Architecture.X86 => "x86",
            Architecture.Arm64 => "arm64",
            _ => string.Empty,
        };
        if (architecture.Length > 0 && normalized.Contains($"win-{architecture}")) score += 16;
        else if (normalized.Contains("win-x64")) score += 8;
        else if (normalized.Contains("windows") || normalized.Contains("win")) score += 4;
        if (normalized.EndsWith(".zip")) score += 2;
        return score;
    }
}
