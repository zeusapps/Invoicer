using System.Diagnostics;
using System.IO.Compression;

namespace Invoicer.Update;

public class InstallResult
{
    public bool Success { get; init; }
    public string Error { get; init; } = "";

    public static InstallResult Ok() => new() { Success = true };
    public static InstallResult Fail(string error) => new() { Success = false, Error = error };
}

/// <summary>
/// Downloads a release asset and swaps it in for the running executable.
///
/// Windows refuses to delete or overwrite the image of a running process, but it does allow
/// renaming it within the same volume. So the running exe is renamed aside, the new one moved
/// into its place, and the leftover deleted on the next launch — no helper process, no
/// elevation, and the original binary is restored if anything fails partway.
/// </summary>
public static class UpdateInstaller
{
    /// <summary>Suffix given to the superseded executable; <see cref="CleanupPreviousUpdate"/> removes it.</summary>
    public const string BackupSuffix = ".old";

    private const string ExecutableName = "Invoicer.exe";

    /// <summary>
    /// Downloads, verifies, and swaps in the release asset. On success the new executable is
    /// started and the caller should shut the TUI down.
    /// </summary>
    public static async Task<InstallResult> InstallAsync(
        ReleaseInfo release,
        CancellationToken cancellationToken = default)
    {
        if (!release.HasAsset)
            return InstallResult.Fail("The release has no downloadable executable.");

        var currentExecutable = CurrentExecutablePath();
        if (string.IsNullOrEmpty(currentExecutable))
            return InstallResult.Fail("Could not determine the path of the running executable.");

        var workingDirectory = Path.Combine(
            Path.GetTempPath(),
            "invoicer-update",
            Guid.NewGuid().ToString("N"));

        try
        {
            Directory.CreateDirectory(workingDirectory);

            var archivePath = Path.Combine(workingDirectory, "release.zip");
            var download = await DownloadAsync(release.AssetUrl, archivePath, cancellationToken);
            if (!download.Success)
                return download;

            var extractedPath = Path.Combine(workingDirectory, ExecutableName);
            var extraction = Extract(archivePath, extractedPath);
            if (!extraction.Success)
                return extraction;

            var swap = Swap(currentExecutable, extractedPath);
            if (!swap.Success)
                return swap;

            Launch(currentExecutable);
            return InstallResult.Ok();
        }
        catch (Exception ex)
        {
            return InstallResult.Fail(ex.Message);
        }
        finally
        {
            TryDeleteDirectory(workingDirectory);
        }
    }

    internal static async Task<InstallResult> DownloadAsync(
        string assetUrl,
        string destinationPath,
        CancellationToken cancellationToken)
    {
        try
        {
            using var client = UpdateChecker.CreateClient();
            // The asset is tens of MB; the checker's timeout is sized for a JSON response.
            client.Timeout = TimeSpan.FromMinutes(10);

            using var response = await client.GetAsync(
                assetUrl,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);

            if (!response.IsSuccessStatusCode)
                return InstallResult.Fail($"Download failed: {(int)response.StatusCode} {response.StatusCode}.");

            await using var source = await response.Content.ReadAsStreamAsync(cancellationToken);
            await using var target = File.Create(destinationPath);
            await source.CopyToAsync(target, cancellationToken);

            return InstallResult.Ok();
        }
        catch (OperationCanceledException)
        {
            return InstallResult.Fail("The download was cancelled.");
        }
        catch (Exception ex)
        {
            return InstallResult.Fail($"Download failed: {ex.Message}");
        }
    }

    internal static InstallResult Extract(string archivePath, string destinationPath)
    {
        try
        {
            using var archive = ZipFile.OpenRead(archivePath);

            var entry = archive.Entries.FirstOrDefault(e =>
                             string.Equals(e.Name, ExecutableName, StringComparison.OrdinalIgnoreCase))
                        ?? archive.Entries.SingleOrDefault(e =>
                             e.Name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase));

            if (entry is null)
                return InstallResult.Fail($"The downloaded archive does not contain {ExecutableName}.");

            entry.ExtractToFile(destinationPath, overwrite: true);
        }
        catch (InvalidDataException)
        {
            return InstallResult.Fail("The downloaded file is not a valid archive.");
        }
        catch (Exception ex)
        {
            return InstallResult.Fail($"Could not extract the update: {ex.Message}");
        }

        return IsWindowsExecutable(destinationPath)
            ? InstallResult.Ok()
            : InstallResult.Fail("The extracted file is not a valid Windows executable.");
    }

    /// <summary>Cheap sanity check that the extracted file is a real PE image before it replaces a working binary.</summary>
    internal static bool IsWindowsExecutable(string path)
    {
        try
        {
            var info = new FileInfo(path);
            if (!info.Exists || info.Length < 64)
                return false;

            using var stream = File.OpenRead(path);
            return stream.ReadByte() == 'M' && stream.ReadByte() == 'Z';
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Renames the running executable aside and moves the replacement into its place,
    /// restoring the original if the second move fails.
    /// </summary>
    internal static InstallResult Swap(string currentExecutable, string replacement)
    {
        if (!File.Exists(replacement))
            return InstallResult.Fail("The downloaded executable is missing.");

        var backupPath = currentExecutable + BackupSuffix;

        try
        {
            // A leftover from an interrupted update would block the rename.
            if (File.Exists(backupPath))
                File.Delete(backupPath);
        }
        catch
        {
            // Non-fatal on its own; the rename below reports the real problem.
        }

        try
        {
            File.Move(currentExecutable, backupPath);
        }
        catch (Exception ex)
        {
            return InstallResult.Fail($"Could not set the current version aside: {ex.Message}");
        }

        try
        {
            File.Move(replacement, currentExecutable);
            return InstallResult.Ok();
        }
        catch (Exception ex)
        {
            TryRestore(backupPath, currentExecutable);
            return InstallResult.Fail($"Could not install the new version: {ex.Message}");
        }
    }

    /// <summary>Deletes the executable left behind by a previous update. Never throws.</summary>
    public static void CleanupPreviousUpdate()
    {
        try
        {
            var backupPath = CurrentExecutablePath() + BackupSuffix;
            if (!string.IsNullOrEmpty(backupPath) && File.Exists(backupPath))
                File.Delete(backupPath);
        }
        catch
        {
            // A locked leftover must never stop the app from starting; the next launch retries.
        }
    }

    /// <summary>
    /// Path of the running executable. Under PublishSingleFile this is the bundle itself;
    /// ProcessPath is the only API that reports it correctly.
    /// </summary>
    internal static string CurrentExecutablePath() => Environment.ProcessPath ?? "";

    private static void Launch(string executablePath)
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = executablePath,
            WorkingDirectory = Path.GetDirectoryName(executablePath) ?? "",
            UseShellExecute = true,
        });
    }

    private static void TryRestore(string backupPath, string originalPath)
    {
        try
        {
            if (File.Exists(backupPath) && !File.Exists(originalPath))
                File.Move(backupPath, originalPath);
        }
        catch
        {
            // Nothing further can be done here; the caller reports the failure and the
            // .old file remains beside the executable for manual recovery.
        }
    }

    private static void TryDeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
                Directory.Delete(path, recursive: true);
        }
        catch
        {
            // Temp files are the OS's problem if we cannot clean them up.
        }
    }
}
