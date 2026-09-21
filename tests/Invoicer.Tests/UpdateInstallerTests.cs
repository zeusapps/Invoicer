using System.IO.Compression;
using Invoicer.Update;
using Xunit;

namespace Invoicer.Tests;

public class UpdateInstallerTests
{
    /// <summary>Minimal PE-looking payload: the installer only checks for the MZ header and a plausible length.</summary>
    private static byte[] FakeExecutable(string marker)
    {
        var bytes = new byte[128];
        bytes[0] = (byte)'M';
        bytes[1] = (byte)'Z';
        System.Text.Encoding.ASCII.GetBytes(marker).CopyTo(bytes, 2);
        return bytes;
    }

    [Fact]
    public void Swap_ReplacesTheCurrentExecutable()
    {
        WithTempDir(dir =>
        {
            var current = Path.Combine(dir, "Invoicer.exe");
            var replacement = Path.Combine(dir, "new", "Invoicer.exe");
            Directory.CreateDirectory(Path.GetDirectoryName(replacement)!);
            File.WriteAllBytes(current, FakeExecutable("old"));
            File.WriteAllBytes(replacement, FakeExecutable("new"));

            var result = UpdateInstaller.Swap(current, replacement);

            Assert.True(result.Success, result.Error);
            Assert.Equal(FakeExecutable("new"), File.ReadAllBytes(current));
            Assert.True(File.Exists(current + UpdateInstaller.BackupSuffix));
            Assert.Equal(FakeExecutable("old"), File.ReadAllBytes(current + UpdateInstaller.BackupSuffix));
        });
    }

    [Fact]
    public void Swap_MissingReplacement_LeavesCurrentExecutableAlone()
    {
        WithTempDir(dir =>
        {
            var current = Path.Combine(dir, "Invoicer.exe");
            File.WriteAllBytes(current, FakeExecutable("old"));

            var result = UpdateInstaller.Swap(current, Path.Combine(dir, "does-not-exist.exe"));

            Assert.False(result.Success);
            Assert.Equal(FakeExecutable("old"), File.ReadAllBytes(current));
            Assert.False(File.Exists(current + UpdateInstaller.BackupSuffix));
        });
    }

    [Fact]
    public void Swap_FailureMovingNewFileIntoPlace_RestoresTheOriginal()
    {
        WithTempDir(dir =>
        {
            var current = Path.Combine(dir, "Invoicer.exe");
            var replacement = Path.Combine(dir, "new", "Invoicer.exe");
            Directory.CreateDirectory(Path.GetDirectoryName(replacement)!);
            File.WriteAllBytes(current, FakeExecutable("old"));
            File.WriteAllBytes(replacement, FakeExecutable("new"));

            // Hold the replacement open so the move into place fails after the rename has happened.
            using (File.Open(replacement, FileMode.Open, FileAccess.Read, FileShare.None))
            {
                var result = UpdateInstaller.Swap(current, replacement);

                Assert.False(result.Success);
                Assert.Contains("Could not install the new version", result.Error);
            }

            Assert.True(File.Exists(current));
            Assert.Equal(FakeExecutable("old"), File.ReadAllBytes(current));
            Assert.False(File.Exists(current + UpdateInstaller.BackupSuffix));
        });
    }

    [Fact]
    public void Swap_LeftoverBackupFromAnEarlierUpdate_DoesNotBlockTheRename()
    {
        WithTempDir(dir =>
        {
            var current = Path.Combine(dir, "Invoicer.exe");
            var replacement = Path.Combine(dir, "new", "Invoicer.exe");
            Directory.CreateDirectory(Path.GetDirectoryName(replacement)!);
            File.WriteAllBytes(current, FakeExecutable("old"));
            File.WriteAllBytes(replacement, FakeExecutable("new"));
            File.WriteAllBytes(current + UpdateInstaller.BackupSuffix, FakeExecutable("ancient"));

            var result = UpdateInstaller.Swap(current, replacement);

            Assert.True(result.Success, result.Error);
            Assert.Equal(FakeExecutable("new"), File.ReadAllBytes(current));
        });
    }

    // A failure to start the new executable is not a failed install: the swap has already
    // happened. Reporting it as a failure told the user "Invoicer is unchanged" about an
    // executable that had just been replaced.
    [Fact]
    public void SwapAndLaunch_WhenLaunchFails_ReportsInstalledButNotRelaunched()
    {
        WithTempDir(dir =>
        {
            var current = Path.Combine(dir, "Invoicer.exe");
            var replacement = Path.Combine(dir, "new", "Invoicer.exe");
            Directory.CreateDirectory(Path.GetDirectoryName(replacement)!);
            File.WriteAllBytes(current, FakeExecutable("old"));
            File.WriteAllBytes(replacement, FakeExecutable("new"));

            var result = UpdateInstaller.SwapAndLaunch(
                current, replacement, _ => throw new InvalidOperationException("shell refused"));

            Assert.True(result.Success);
            Assert.False(result.Relaunched);
            Assert.Equal("shell refused", result.Error);
            // The whole point: the update really is installed.
            Assert.Equal(FakeExecutable("new"), File.ReadAllBytes(current));
        });
    }

    [Fact]
    public void SwapAndLaunch_WhenLaunchSucceeds_ReportsRelaunched()
    {
        WithTempDir(dir =>
        {
            var current = Path.Combine(dir, "Invoicer.exe");
            var replacement = Path.Combine(dir, "new", "Invoicer.exe");
            Directory.CreateDirectory(Path.GetDirectoryName(replacement)!);
            File.WriteAllBytes(current, FakeExecutable("old"));
            File.WriteAllBytes(replacement, FakeExecutable("new"));

            var launched = "";
            var result = UpdateInstaller.SwapAndLaunch(current, replacement, path => launched = path);

            Assert.True(result.Success);
            Assert.True(result.Relaunched);
            Assert.Equal("", result.Error);
            Assert.Equal(current, launched);
        });
    }

    [Fact]
    public void SwapAndLaunch_WhenSwapFails_DoesNotLaunchAndReportsFailure()
    {
        WithTempDir(dir =>
        {
            var current = Path.Combine(dir, "Invoicer.exe");
            File.WriteAllBytes(current, FakeExecutable("old"));

            var launched = false;
            var result = UpdateInstaller.SwapAndLaunch(
                current, Path.Combine(dir, "missing.exe"), _ => launched = true);

            Assert.False(result.Success);
            Assert.False(result.Relaunched);
            Assert.False(launched);
            // Nothing was touched, so "Invoicer is unchanged" is accurate here.
            Assert.Equal(FakeExecutable("old"), File.ReadAllBytes(current));
        });
    }

    [Fact]
    public void Extract_PullsTheExecutableFromTheArchive()
    {
        WithTempDir(dir =>
        {
            var archive = Path.Combine(dir, "release.zip");
            CreateArchive(archive, ("Invoicer.exe", FakeExecutable("published")));
            var destination = Path.Combine(dir, "extracted.exe");

            var result = UpdateInstaller.Extract(archive, destination);

            Assert.True(result.Success, result.Error);
            Assert.Equal(FakeExecutable("published"), File.ReadAllBytes(destination));
        });
    }

    [Fact]
    public void Extract_ArchiveWithoutAnExecutable_Fails()
    {
        WithTempDir(dir =>
        {
            var archive = Path.Combine(dir, "release.zip");
            CreateArchive(archive, ("readme.txt", "not an executable"u8.ToArray()));

            var result = UpdateInstaller.Extract(archive, Path.Combine(dir, "extracted.exe"));

            Assert.False(result.Success);
            Assert.Contains("does not contain", result.Error);
        });
    }

    [Fact]
    public void Extract_RejectsAFileThatIsNotAWindowsExecutable()
    {
        WithTempDir(dir =>
        {
            var archive = Path.Combine(dir, "release.zip");
            // Right name, wrong content — an HTML error page saved under the expected filename.
            CreateArchive(archive, ("Invoicer.exe", "<html>404</html>"u8.ToArray()));

            var result = UpdateInstaller.Extract(archive, Path.Combine(dir, "extracted.exe"));

            Assert.False(result.Success);
            Assert.Contains("not a valid Windows executable", result.Error);
        });
    }

    [Fact]
    public void Extract_CorruptArchive_Fails()
    {
        WithTempDir(dir =>
        {
            var archive = Path.Combine(dir, "release.zip");
            File.WriteAllText(archive, "this is not a zip file");

            var result = UpdateInstaller.Extract(archive, Path.Combine(dir, "extracted.exe"));

            Assert.False(result.Success);
            Assert.Contains("not a valid archive", result.Error);
        });
    }

    [Fact]
    public void IsWindowsExecutable_RejectsEmptyAndMissingFiles()
    {
        WithTempDir(dir =>
        {
            var empty = Path.Combine(dir, "empty.exe");
            File.WriteAllBytes(empty, []);

            Assert.False(UpdateInstaller.IsWindowsExecutable(empty));
            Assert.False(UpdateInstaller.IsWindowsExecutable(Path.Combine(dir, "missing.exe")));
        });
    }

    [Fact]
    public async Task InstallAsync_ReleaseWithoutAnAsset_FailsWithoutTouchingAnything()
    {
        var result = await UpdateInstaller.InstallAsync(new ReleaseInfo { Tag = "v9.9.9" });

        Assert.False(result.Success);
        Assert.Contains("no downloadable executable", result.Error);
    }

    private static void CreateArchive(string path, params (string Name, byte[] Content)[] entries)
    {
        using var stream = File.Create(path);
        using var archive = new ZipArchive(stream, ZipArchiveMode.Create);

        foreach (var (name, content) in entries)
        {
            var entry = archive.CreateEntry(name);
            using var entryStream = entry.Open();
            entryStream.Write(content);
        }
    }

    private static void WithTempDir(Action<string> body)
    {
        var dir = Path.Combine(Path.GetTempPath(), "invoicer-update-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);

        try
        {
            body(dir);
        }
        finally
        {
            try
            {
                Directory.Delete(dir, recursive: true);
            }
            catch
            {
                // Best effort.
            }
        }
    }
}
