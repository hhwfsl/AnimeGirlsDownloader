using AnimeGirlsDownloader.Enums;
using AnimeGirlsDownloader.Interfaces;
using AnimeGirlsDownloader.Models;
using AnimeGirlsDownloader.Requests;
using AnimeGirlsDownloader.Responses;
using AnimeGirlsDownloader.Serialization;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using System.Threading;
using System.Threading.Tasks;

namespace AnimeGirlsDownloader.Services;

public sealed class AnimeGirlsApiClient : IAnimeGirlsApiClient
{
    private readonly HttpClient _httpClient;
    private readonly ITokenStore _tokenStore;

    public AnimeGirlsApiClient(HttpClient httpClient, ITokenStore tokenStore)
    {
        _httpClient = httpClient;
        _tokenStore = tokenStore;
    }

    public async Task<bool> LoginAsync(
        string userName,
        string password,
        CancellationToken cancellationToken = default)
    {
        var request = new LoginRequest { UserName = userName, Password = password };
        LoginResponse response = await PostAsync(
            "auth/login",
            request,
            AppJsonSerializerContext.Default.LoginRequest,
            AppJsonSerializerContext.Default.LoginResponse,
            cancellationToken: cancellationToken);

        await StoreTokenAsync(response.Token, cancellationToken);
        return true;
    }

    public async Task<bool> LoginWithStoredTokenAsync(CancellationToken cancellationToken = default)
    {
        string? token = await _tokenStore.ReadAsync(cancellationToken);
        if (token is null)
        {
            return false;
        }

        try
        {
            LoginResponse response = await PostAsync<object, LoginResponse>(
                "auth/login-with-token",
                payload: null,
                requestTypeInfo: null,
                AppJsonSerializerContext.Default.LoginResponse,
                token,
                cancellationToken);

            await StoreTokenAsync(response.Token, cancellationToken);
            return true;
        }
        catch (ApiClientException exception) when (exception.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
        {
            _tokenStore.Delete();
            return false;
        }
    }

    public async Task<bool> RegisterAsync(
        string userName,
        string password,
        CancellationToken cancellationToken = default)
    {
        var request = new RegisterRequest { UserName = userName, Password = password };
        RegisterResponse response = await PostAsync(
            "auth/register",
            request,
            AppJsonSerializerContext.Default.RegisterRequest,
            AppJsonSerializerContext.Default.RegisterResponse,
            cancellationToken: cancellationToken);

        await StoreTokenAsync(response.Token, cancellationToken);
        return true;
    }

    public async Task<GetImageResponse> GetImageByIdAsync(long imageId, CancellationToken cancellationToken = default)
    {
        var request = new GetImageRequest { Id = imageId };
        GetImageResponse response = await PostAsync(
            "image/id",
            request,
            AppJsonSerializerContext.Default.GetImageRequest,
            AppJsonSerializerContext.Default.GetImageResponse,
            cancellationToken: cancellationToken);
        NormalizeImageUrls(response);
        return response;
    }

    public async Task<GetImageResponse> GetRandomImageAsync(
        IReadOnlyCollection<Tag>? tags,
        ImageType type,
        ImageIsAllowAiType isAllowAiGenerated,
        CancellationToken cancellationToken = default)
    {
        var request = new GetImageRequest
        {
            Tags = tags?.ToList(),
            Type = type,
            IsAllowAiGenerated = isAllowAiGenerated,
        };

        GetImageResponse response = await PostAsync(
            "image/random",
            request,
            AppJsonSerializerContext.Default.GetImageRequest,
            AppJsonSerializerContext.Default.GetImageResponse,
            cancellationToken: cancellationToken);
        NormalizeImageUrls(response);
        return response;
    }

    public async Task<string> UploadImageAsync(
        UploadImageRequest request,
        CancellationToken cancellationToken = default)
    {
        string token = await RequireTokenAsync(cancellationToken);
        MessageResponse response = await PostAsync(
            "image/upload",
            request,
            AppJsonSerializerContext.Default.UploadImageRequest,
            AppJsonSerializerContext.Default.MessageResponse,
            token,
            cancellationToken);

        return response.Message;
    }

    public async Task<IReadOnlyList<Tag>> GetTagsAsync(
        string partialTag,
        CancellationToken cancellationToken = default)
    {
        string token = await RequireTokenAsync(cancellationToken);
        var request = new TagRequest { PartialTag = partialTag };
        TagResponse response = await PostAsync(
            "image/tag/fulltags",
            request,
            AppJsonSerializerContext.Default.TagRequest,
            AppJsonSerializerContext.Default.TagResponse,
            token,
            cancellationToken);

        return response.Tags;
    }

    public async Task<UserProfileResponse> GetCurrentUserAsync(CancellationToken cancellationToken = default)
    {
        string token = await RequireTokenAsync(cancellationToken);
        UserProfileResponse profile = await GetAsync(
            "auth/me",
            AppJsonSerializerContext.Default.UserProfileResponse,
            token,
            cancellationToken);
        if (!string.IsNullOrWhiteSpace(profile.AvatarUrl))
        {
            profile.AvatarUrl = MakeAbsoluteUrl(profile.AvatarUrl);
        }
        return profile;
    }

    public async Task<byte[]?> DownloadAvatarAsync(string avatarUrl, CancellationToken cancellationToken = default)
    {
        string token = await RequireTokenAsync(cancellationToken);
        using var request = new HttpRequestMessage(HttpMethod.Get, MakeAbsoluteUrl(avatarUrl));
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        using HttpResponseMessage response = await _httpClient.SendAsync(request, cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound) return null;
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsByteArrayAsync(cancellationToken);
    }

    public async Task<string> DownloadImageAsync(
        string downloadUrl,
        string destinationDirectory,
        string fallbackFileName,
        IProgress<double>? progress = null,
        CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(destinationDirectory);
        using var request = new HttpRequestMessage(HttpMethod.Get, MakeAbsoluteUrl(downloadUrl));
        using HttpResponseMessage response = await _httpClient.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);
        response.EnsureSuccessStatusCode();

        string suggestedName = response.Content.Headers.ContentDisposition?.FileNameStar
            ?? response.Content.Headers.ContentDisposition?.FileName
            ?? fallbackFileName;
        string fileName = SanitizeFileName(suggestedName.Trim('"'));
        string destinationPath = GetAvailablePath(destinationDirectory, fileName);
        string temporaryPath = destinationPath + ".part";
        long? totalBytes = response.Content.Headers.ContentLength;

        try
        {
            // Dispose both streams before moving or deleting the temporary file. Keeping the
            // output stream in the outer method scope leaves the .part file locked on Windows.
            await using (Stream input = await response.Content.ReadAsStreamAsync(cancellationToken))
            await using (FileStream output = new(
                temporaryPath,
                FileMode.Create,
                FileAccess.Write,
                FileShare.None,
                81920,
                FileOptions.Asynchronous | FileOptions.SequentialScan))
            {
                byte[] buffer = new byte[81920];
                long receivedBytes = 0;
                int bytesRead;
                while ((bytesRead = await input.ReadAsync(buffer, cancellationToken)) > 0)
                {
                    await output.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken);
                    receivedBytes += bytesRead;
                    if (totalBytes is > 0)
                        progress?.Report(receivedBytes * 100d / totalBytes.Value);
                }
                await output.FlushAsync(cancellationToken);
            }

            cancellationToken.ThrowIfCancellationRequested();
            File.Move(temporaryPath, destinationPath, overwrite: false);
            progress?.Report(100);
            return destinationPath;
        }
        catch
        {
            TryDeleteTemporaryFile(temporaryPath);
            throw;
        }
    }

    private async Task<string> RequireTokenAsync(CancellationToken cancellationToken)
    {
        return await _tokenStore.ReadAsync(cancellationToken)
            ?? throw new ApiClientException("Please sign in before performing this operation.", HttpStatusCode.Unauthorized);
    }

    private async Task StoreTokenAsync(string token, CancellationToken cancellationToken)
    {
        try
        {
            await _tokenStore.WriteAsync(token, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            throw new ApiClientException("Sign-in succeeded, but the token could not be stored securely.", innerException: exception);
        }
    }

    private async Task<TResponse> GetAsync<TResponse>(
        string relativeUri,
        JsonTypeInfo<TResponse> responseTypeInfo,
        string? bearerToken,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, relativeUri);
        if (!string.IsNullOrWhiteSpace(bearerToken))
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken);
        try
        {
            using HttpResponseMessage response = await _httpClient.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);
            if (!response.IsSuccessStatusCode)
                throw new ApiClientException($"The server returned {(int)response.StatusCode} ({response.ReasonPhrase}).", response.StatusCode);
            await using Stream stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            return await JsonSerializer.DeserializeAsync(stream, responseTypeInfo, cancellationToken)
                ?? throw new ApiClientException("The server returned an empty or invalid response.");
        }
        catch (ApiClientException) { throw; }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        { throw new ApiClientException("The request timed out."); }
        catch (HttpRequestException exception)
        { throw new ApiClientException("Unable to connect to the server.", innerException: exception); }
    }

    private void NormalizeImageUrls(GetImageResponse response)
    {
        response.PreviewUrl = MakeAbsoluteUrl(response.PreviewUrl);
        response.DownloadUrl = MakeAbsoluteUrl(response.DownloadUrl);
    }

    private string MakeAbsoluteUrl(string url) => Uri.TryCreate(url, UriKind.Absolute, out Uri? absolute)
        ? absolute.AbsoluteUri
        : new Uri(_httpClient.BaseAddress!, url).AbsoluteUri;

    private static string SanitizeFileName(string value)
    {
        string result = Path.GetFileName(value);
        foreach (char invalid in Path.GetInvalidFileNameChars()) result = result.Replace(invalid, '_');
        return string.IsNullOrWhiteSpace(result) ? "image" : result;
    }

    private static string GetAvailablePath(string directory, string fileName)
    {
        string path = Path.Combine(directory, fileName);
        if (!File.Exists(path) && !File.Exists(path + ".part")) return path;
        string baseName = Path.GetFileNameWithoutExtension(fileName);
        string extension = Path.GetExtension(fileName);
        for (int index = 1; ; index++)
        {
            path = Path.Combine(directory, $"{baseName} ({index}){extension}");
            if (!File.Exists(path) && !File.Exists(path + ".part")) return path;
        }
    }

    private static void TryDeleteTemporaryFile(string path)
    {
        try
        {
            if (File.Exists(path)) File.Delete(path);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            AppLogger.LogWarning($"Unable to remove the incomplete download '{path}': {exception.Message}");
        }
    }

    private async Task<TResponse> PostAsync<TRequest, TResponse>(
        string relativeUri,
        TRequest? payload,
        JsonTypeInfo<TRequest>? requestTypeInfo,
        JsonTypeInfo<TResponse> responseTypeInfo,
        string? bearerToken = null,
        CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, relativeUri);
        if (payload is not null && requestTypeInfo is not null)
        {
            request.Content = JsonContent.Create(payload, requestTypeInfo);
        }

        if (!string.IsNullOrWhiteSpace(bearerToken))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken);
        }

        try
        {
            using HttpResponseMessage response = await _httpClient.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                throw new ApiClientException(
                    $"The server returned {(int)response.StatusCode} ({response.ReasonPhrase}).",
                    response.StatusCode);
            }

            await using System.IO.Stream responseStream = await response.Content.ReadAsStreamAsync(cancellationToken);
            TResponse? result = await JsonSerializer.DeserializeAsync(
                responseStream,
                responseTypeInfo,
                cancellationToken);
            return result ?? throw new ApiClientException("The server returned an empty or invalid response.");
        }
        catch (ApiClientException)
        {
            throw;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new ApiClientException("The request timed out.");
        }
        catch (HttpRequestException exception)
        {
            throw new ApiClientException("Unable to connect to the server.", innerException: exception);
        }
        catch (JsonException exception)
        {
            throw new ApiClientException("The server returned an invalid response.", innerException: exception);
        }
    }
}
