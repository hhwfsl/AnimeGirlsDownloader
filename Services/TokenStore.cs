using AnimeGirlsDownloader.Interfaces;
using System;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Windows.Security.Cryptography;
using Windows.Security.Cryptography.DataProtection;
using Windows.Storage.Streams;

namespace AnimeGirlsDownloader.Services;

public sealed class TokenStore : ITokenStore
{
    private const string ProtectionDescriptor = "LOCAL=user";
    private readonly SemaphoreSlim _gate = new(1, 1);
    private long? _activeUserId;
    private string? _pendingToken;

    public TokenStore()
    {
        AppPaths.EnsureDataDirectories();
        _activeUserId = ReadActiveUserId();
    }

    public async Task<string?> ReadAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (!string.IsNullOrWhiteSpace(_pendingToken)) return _pendingToken;

            if (_activeUserId is not long userId)
                return await ReadLegacyTokenIntoMemoryAsync(cancellationToken);

            string tokenPath = AppPaths.GetUserTokenFilePath(userId);
            if (!File.Exists(tokenPath))
            {
                ClearActiveUser();
                return await ReadLegacyTokenIntoMemoryAsync(cancellationToken);
            }

            try
            {
                return await ReadProtectedTokenAsync(tokenPath, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception exception)
            {
                AppLogger.LogWarning($"The stored sign-in token could not be read and was removed: {exception.Message}");
                DeleteFile(tokenPath);
                ClearActiveUser();
                return null;
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task WriteAsync(string token, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(token);
        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (_activeUserId is long userId)
                await WriteProtectedTokenAsync(userId, token, cancellationToken);
            else
                _pendingToken = token;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task ActivateUserAsync(long userId, CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(userId);
        await _gate.WaitAsync(cancellationToken);
        try
        {
            AppPaths.EnsureUserDirectory(userId);
            if (!string.IsNullOrWhiteSpace(_pendingToken))
                await WriteProtectedTokenAsync(userId, _pendingToken, cancellationToken);

            if (!File.Exists(AppPaths.GetUserTokenFilePath(userId)))
                throw new InvalidOperationException("No sign-in token is available for the authenticated user.");

            WriteActiveUser(userId);
            _activeUserId = userId;
            _pendingToken = null;
            DeleteFile(AppPaths.LegacyTokenFilePath);
            MarkLegacyTokenAsMigrated();
        }
        finally
        {
            _gate.Release();
        }
    }

    public void Delete()
    {
        _gate.Wait();
        try
        {
            if (_activeUserId is long userId)
                DeleteFile(AppPaths.GetUserTokenFilePath(userId));
            _pendingToken = null;
            ClearActiveUser();
            DeleteFile(AppPaths.LegacyTokenFilePath);
            MarkLegacyTokenAsMigrated();
        }
        finally
        {
            _gate.Release();
        }
    }

    private static async Task<string> ReadProtectedTokenAsync(string path, CancellationToken cancellationToken)
    {
        byte[] encryptedBytes = await File.ReadAllBytesAsync(path, cancellationToken);
        IBuffer encryptedBuffer = CryptographicBuffer.CreateFromByteArray(encryptedBytes);
        var provider = new DataProtectionProvider();
        IBuffer clearBuffer = await provider.UnprotectAsync(encryptedBuffer).AsTask(cancellationToken);
        string token = CryptographicBuffer.ConvertBinaryToString(BinaryStringEncoding.Utf8, clearBuffer).Trim();
        return string.IsNullOrWhiteSpace(token)
            ? throw new InvalidDataException("The token file is empty.")
            : token;
    }

    private static async Task WriteProtectedTokenAsync(long userId, string token, CancellationToken cancellationToken)
    {
        AppPaths.EnsureUserDirectory(userId);
        IBuffer clearBuffer = CryptographicBuffer.ConvertStringToBinary(token, BinaryStringEncoding.Utf8);
        var provider = new DataProtectionProvider(ProtectionDescriptor);
        IBuffer encryptedBuffer = await provider.ProtectAsync(clearBuffer).AsTask(cancellationToken);
        CryptographicBuffer.CopyToByteArray(encryptedBuffer, out byte[] encryptedBytes);

        string tokenPath = AppPaths.GetUserTokenFilePath(userId);
        string temporaryPath = tokenPath + ".tmp";
        try
        {
            await File.WriteAllBytesAsync(temporaryPath, encryptedBytes, cancellationToken);
            File.Move(temporaryPath, tokenPath, overwrite: true);
        }
        finally
        {
            DeleteFile(temporaryPath);
        }
    }

    private async Task<string?> ReadLegacyTokenIntoMemoryAsync(CancellationToken cancellationToken)
    {
        string[] candidates = File.Exists(AppPaths.LegacyTokenMigrationMarkerFilePath)
            ? new[] { AppPaths.LegacyTokenFilePath }
            : new[] { AppPaths.PreviousTokenFilePath, AppPaths.LegacyTokenFilePath };
        foreach (string path in candidates)
        {
            if (!File.Exists(path)) continue;
            try
            {
                string token;
                try
                {
                    token = await ReadProtectedTokenAsync(path, cancellationToken);
                }
                catch (Exception exception) when (exception is not OperationCanceledException)
                {
                    token = (await File.ReadAllTextAsync(path, Encoding.UTF8, cancellationToken)).Trim();
                }

                if (!string.IsNullOrWhiteSpace(token))
                {
                    _pendingToken = token;
                    return token;
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception exception)
            {
                AppLogger.LogWarning($"Legacy sign-in token could not be migrated: {exception.Message}");
            }
        }
        return null;
    }

    private static long? ReadActiveUserId()
    {
        try
        {
            if (!File.Exists(AppPaths.ActiveUserFilePath)) return null;
            string value = File.ReadAllText(AppPaths.ActiveUserFilePath).Trim();
            if (long.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out long userId) && userId > 0)
                return userId;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            AppLogger.LogWarning($"The active user marker could not be read: {exception.Message}");
        }
        DeleteFile(AppPaths.ActiveUserFilePath);
        return null;
    }

    private static void WriteActiveUser(long userId)
    {
        string temporaryPath = AppPaths.ActiveUserFilePath + ".tmp";
        try
        {
            File.WriteAllText(temporaryPath, userId.ToString(CultureInfo.InvariantCulture));
            File.Move(temporaryPath, AppPaths.ActiveUserFilePath, overwrite: true);
        }
        finally
        {
            DeleteFile(temporaryPath);
        }
    }

    private static void MarkLegacyTokenAsMigrated()
    {
        try
        {
            File.WriteAllText(AppPaths.LegacyTokenMigrationMarkerFilePath, "1");
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            AppLogger.LogWarning($"The legacy token migration marker could not be written: {exception.Message}");
        }
    }

    private void ClearActiveUser()
    {
        _activeUserId = null;
        DeleteFile(AppPaths.ActiveUserFilePath);
    }

    private static void DeleteFile(string path)
    {
        try
        {
            if (File.Exists(path)) File.Delete(path);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            AppLogger.LogWarning($"A local account file could not be removed: {exception.Message}");
        }
    }
}
