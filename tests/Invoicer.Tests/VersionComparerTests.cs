using Invoicer.Update;
using Xunit;

namespace Invoicer.Tests;

public class VersionComparerTests
{
    [Theory]
    [InlineData("v1.2.0", 1, 2, 0)]
    [InlineData("1.2.0", 1, 2, 0)]
    [InlineData("V1.2.0", 1, 2, 0)]
    [InlineData("  v1.2.0  ", 1, 2, 0)]
    [InlineData("v1.2", 1, 2, 0)]
    [InlineData("v10.20.30", 10, 20, 30)]
    public void TryParseTag_AcceptsVersionTags(string tag, int major, int minor, int patch)
    {
        Assert.True(VersionComparer.TryParseTag(tag, out var version));
        Assert.Equal(new Version(major, minor, patch), version);
    }

    [Theory]
    [InlineData("nightly")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    [InlineData("v")]
    [InlineData("release-2026-08")]
    [InlineData("v1.2.0-beta")]
    public void TryParseTag_RejectsNonVersionTags(string? tag)
    {
        Assert.False(VersionComparer.TryParseTag(tag, out _));
    }

    [Fact]
    public void IsNewer_TrueForHigherVersion()
    {
        Assert.True(VersionComparer.IsNewer(new Version(1, 2, 0), new Version(1, 1, 1)));
        Assert.True(VersionComparer.IsNewer(new Version(2, 0, 0), new Version(1, 9, 9)));
        Assert.True(VersionComparer.IsNewer(new Version(1, 1, 2), new Version(1, 1, 1)));
    }

    [Fact]
    public void IsNewer_FalseForEqualVersion()
    {
        Assert.False(VersionComparer.IsNewer(new Version(1, 2, 0), new Version(1, 2, 0)));
    }

    [Fact]
    public void IsNewer_IgnoresAssemblyRevisionComponent()
    {
        // Assembly versions carry a fourth component; release tags never do.
        VersionComparer.TryParseTag("v1.2.0", out var tagVersion);
        var runningAssemblyVersion = new Version(1, 2, 0, 0);

        Assert.False(VersionComparer.IsNewer(tagVersion, runningAssemblyVersion));
    }

    [Fact]
    public void IsNewer_FalseForOlderVersion()
    {
        Assert.False(VersionComparer.IsNewer(new Version(1, 2, 0), new Version(1, 3, 0)));
    }

    [Fact]
    public void IsNewer_FalseForUnparseableTag()
    {
        Assert.False(VersionComparer.IsNewer(null, new Version(1, 0, 0)));
    }

    [Fact]
    public void IsNewer_AnyReleaseBeatsTheDevelopmentSentinel()
    {
        Assert.True(VersionComparer.IsNewer(new Version(1, 0, 0), new Version(0, 0, 0)));
    }
}
