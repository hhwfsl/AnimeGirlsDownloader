using AnimeGirlsDownloader.Interfaces;
using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace AnimeGirlsDownloader.Services;

public sealed class SelfUpdateService : ISelfUpdateService
{
    private const int MaximumEntryCount = 20_000;
    private const long MaximumExpandedSize = 4L * 1024 * 1024 * 1024;
    private static readonly string[] ProtectedDirectoryNames = ["users", "logs", ".update", "publish"];

    public async Task PrepareAndLaunchAsync(
        string packagePath,
        string version,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(packagePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(version);

        string executablePath = Environment.ProcessPath
            ?? Path.Combine(AppPaths.InstallDirectory, "Anime Girls Downloader.exe");
        string executableName = Path.GetFileName(executablePath);
        string operationDirectory = Path.Combine(
            AppPaths.UpdateStagingDirectory,
            $"{SanitizePathSegment(version)}-{Guid.NewGuid():N}");
        string extractedDirectory = Path.Combine(operationDirectory, "payload");
        string scriptPath = Path.Combine(operationDirectory, "apply-update.ps1");

        Directory.CreateDirectory(extractedDirectory);
        try
        {
            await ExtractAndValidateAsync(
                packagePath,
                extractedDirectory,
                cancellationToken);

            string[] executableCandidates = Directory.GetFiles(
                extractedDirectory,
                executableName,
                SearchOption.AllDirectories);
            if (executableCandidates.Length != 1)
                throw new InvalidDataException("The update package does not contain exactly one application executable.");

            string sourceDirectory = Path.GetDirectoryName(executableCandidates[0])
                ?? throw new InvalidDataException("The update package has an invalid application directory.");
            EnsurePayloadDoesNotContainProtectedDirectories(sourceDirectory);

            await File.WriteAllTextAsync(scriptPath, UpdaterScript, cancellationToken);
            LaunchUpdater(
                scriptPath,
                Environment.ProcessId,
                sourceDirectory,
                AppPaths.InstallDirectory,
                executablePath,
                packagePath,
                operationDirectory,
                Path.Combine(AppPaths.UpdateDirectory, "update-error.log"));
        }
        catch
        {
            TryDeleteDirectory(operationDirectory);
            throw;
        }
    }

    private static async Task ExtractAndValidateAsync(
        string packagePath,
        string destinationDirectory,
        CancellationToken cancellationToken)
    {
        string destinationRoot = Path.GetFullPath(destinationDirectory);
        string destinationPrefix = destinationRoot + Path.DirectorySeparatorChar;
        long expandedSize = 0;
        int entryCount = 0;

        await using var packageStream = new FileStream(
            packagePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            81920,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        using var archive = new ZipArchive(packageStream, ZipArchiveMode.Read, leaveOpen: false);
        foreach (ZipArchiveEntry entry in archive.Entries)
        {
            cancellationToken.ThrowIfCancellationRequested();
            entryCount++;
            if (entryCount > MaximumEntryCount)
                throw new InvalidDataException("The update package contains too many files.");

            expandedSize = checked(expandedSize + entry.Length);
            if (expandedSize > MaximumExpandedSize)
                throw new InvalidDataException("The expanded update package is too large.");

            if (IsSymbolicLink(entry))
                throw new InvalidDataException("The update package contains an unsupported symbolic link.");

            string normalizedName = entry.FullName.Replace('/', Path.DirectorySeparatorChar);
            string targetPath = Path.GetFullPath(Path.Combine(destinationRoot, normalizedName));
            if (!targetPath.StartsWith(destinationPrefix, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("The update package contains an unsafe path.");

            if (entry.FullName.EndsWith('/') || entry.FullName.EndsWith('\\'))
            {
                Directory.CreateDirectory(targetPath);
                continue;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);
            await using Stream source = entry.Open();
            await using var destination = new FileStream(
                targetPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                81920,
                FileOptions.Asynchronous | FileOptions.SequentialScan);
            await source.CopyToAsync(destination, cancellationToken);
        }
    }

    private static bool IsSymbolicLink(ZipArchiveEntry entry) =>
        ((entry.ExternalAttributes >> 16) & 0xF000) == 0xA000;

    private static void EnsurePayloadDoesNotContainProtectedDirectories(string sourceDirectory)
    {
        foreach (string path in Directory.EnumerateDirectories(sourceDirectory, "*", SearchOption.TopDirectoryOnly))
        {
            string name = Path.GetFileName(path);
            if (ProtectedDirectoryNames.Contains(name, StringComparer.OrdinalIgnoreCase))
                throw new InvalidDataException($"The update package contains the protected directory '{name}'.");
        }
    }

    private static void LaunchUpdater(
        string scriptPath,
        int parentProcessId,
        string sourceDirectory,
        string installDirectory,
        string executablePath,
        string packagePath,
        string operationDirectory,
        string errorLogPath)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "powershell.exe",
            UseShellExecute = false,
            CreateNoWindow = true,
            WindowStyle = ProcessWindowStyle.Hidden,
        };
        startInfo.ArgumentList.Add("-NoLogo");
        startInfo.ArgumentList.Add("-NoProfile");
        startInfo.ArgumentList.Add("-NonInteractive");
        startInfo.ArgumentList.Add("-ExecutionPolicy");
        startInfo.ArgumentList.Add("Bypass");
        startInfo.ArgumentList.Add("-File");
        startInfo.ArgumentList.Add(scriptPath);
        startInfo.ArgumentList.Add("-ParentProcessId");
        startInfo.ArgumentList.Add(parentProcessId.ToString(System.Globalization.CultureInfo.InvariantCulture));
        startInfo.ArgumentList.Add("-SourceDirectory");
        startInfo.ArgumentList.Add(sourceDirectory);
        startInfo.ArgumentList.Add("-InstallDirectory");
        startInfo.ArgumentList.Add(installDirectory);
        startInfo.ArgumentList.Add("-ExecutablePath");
        startInfo.ArgumentList.Add(executablePath);
        startInfo.ArgumentList.Add("-PackagePath");
        startInfo.ArgumentList.Add(packagePath);
        startInfo.ArgumentList.Add("-OperationDirectory");
        startInfo.ArgumentList.Add(operationDirectory);
        startInfo.ArgumentList.Add("-ErrorLogPath");
        startInfo.ArgumentList.Add(errorLogPath);

        using Process? process = Process.Start(startInfo);
        if (process is null)
            throw new InvalidOperationException("Unable to start the update helper process.");
    }

    private static string SanitizePathSegment(string value)
    {
        foreach (char invalidCharacter in Path.GetInvalidFileNameChars())
            value = value.Replace(invalidCharacter, '-');
        return value;
    }

    private static void TryDeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path)) Directory.Delete(path, recursive: true);
        }
        catch
        {
            // A failed cleanup must not hide the original update error.
        }
    }

    private const string UpdaterScript = """
param(
    [Parameter(Mandatory = $true)][int]$ParentProcessId,
    [Parameter(Mandatory = $true)][string]$SourceDirectory,
    [Parameter(Mandatory = $true)][string]$InstallDirectory,
    [Parameter(Mandatory = $true)][string]$ExecutablePath,
    [Parameter(Mandatory = $true)][string]$PackagePath,
    [Parameter(Mandatory = $true)][string]$OperationDirectory,
    [Parameter(Mandatory = $true)][string]$ErrorLogPath
)

$ErrorActionPreference = 'Stop'
$backupDirectory = Join-Path $OperationDirectory 'backup'
$copyStarted = $false

function Invoke-FileCopy {
    param(
        [string]$Source,
        [string]$Destination,
        [bool]$ExcludeApplicationData
    )

    New-Item -ItemType Directory -Path $Destination -Force | Out-Null
    $arguments = @(
        $Source,
        $Destination,
        '/E',
        '/COPY:DAT',
        '/DCOPY:DAT',
        '/R:10',
        '/W:1',
        '/NFL',
        '/NDL',
        '/NJH',
        '/NJS',
        '/NP'
    )
    if ($ExcludeApplicationData) {
        $arguments += @(
            '/XD',
            (Join-Path $InstallDirectory 'users'),
            (Join-Path $InstallDirectory 'logs'),
            (Join-Path $InstallDirectory '.update'),
            (Join-Path $InstallDirectory 'publish')
        )
    }

    & robocopy.exe @arguments
    if ($LASTEXITCODE -gt 7) {
        throw "File copy failed with robocopy exit code $LASTEXITCODE."
    }
}

try {
    $deadline = [DateTime]::UtcNow.AddMinutes(2)
    while (Get-Process -Id $ParentProcessId -ErrorAction SilentlyContinue) {
        if ([DateTime]::UtcNow -ge $deadline) {
            throw 'The application did not exit before the update timeout.'
        }
        Start-Sleep -Milliseconds 250
    }

    Invoke-FileCopy -Source $InstallDirectory -Destination $backupDirectory -ExcludeApplicationData $true
    $copyStarted = $true
    Invoke-FileCopy -Source $SourceDirectory -Destination $InstallDirectory -ExcludeApplicationData $false

    Start-Process -FilePath $ExecutablePath -WorkingDirectory $InstallDirectory
    Start-Sleep -Milliseconds 500
    Remove-Item -LiteralPath $PackagePath -Force -ErrorAction SilentlyContinue
    Remove-Item -LiteralPath $OperationDirectory -Recurse -Force -ErrorAction SilentlyContinue
}
catch {
    $failure = $_ | Out-String
    if ($copyStarted -and (Test-Path -LiteralPath $backupDirectory)) {
        try {
            Invoke-FileCopy -Source $backupDirectory -Destination $InstallDirectory -ExcludeApplicationData $false
            $failure += "`r`nThe previous application files were restored."
        }
        catch {
            $failure += "`r`nRollback failed: $($_ | Out-String)"
        }
    }

    $failure | Set-Content -LiteralPath $ErrorLogPath -Encoding UTF8 -Force
    try { Start-Process -FilePath $ExecutablePath -WorkingDirectory $InstallDirectory } catch {}
    exit 1
}
""";
}
