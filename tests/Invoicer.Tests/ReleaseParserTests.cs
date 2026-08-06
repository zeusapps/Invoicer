using Invoicer.Update;
using Xunit;

namespace Invoicer.Tests;

public class ReleaseParserTests
{
    /// <summary>
    /// Captured verbatim from GET /repos/zeusapps/Invoicer/releases/latest so the parser is
    /// exercised against the real payload shape, not an idealised one.
    /// </summary>
    private static string RealPayload =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "TestData", "github-latest-release.json"));

    [Fact]
    public void Parse_RealPayload_ReadsTagAndAsset()
    {
        var release = ReleaseParser.Parse(RealPayload);

        Assert.NotNull(release);
        Assert.Equal("v1.1.1", release.Tag);
        Assert.Equal(new Version(1, 1, 1), release.Version);
        Assert.Equal("v1.1.1", release.Name);
        Assert.Equal("https://github.com/zeusapps/Invoicer/releases/tag/v1.1.1", release.ReleaseUrl);
        Assert.Equal(
            "https://github.com/zeusapps/Invoicer/releases/download/v1.1.1/Invoicer-v1.1.1.zip",
            release.AssetUrl);
        Assert.Equal(16951455, release.AssetSize);
        Assert.True(release.HasAsset);
    }

    [Fact]
    public void Parse_RealPayload_TreatsNullBodyAsEmptyNotes()
    {
        // The live payload really does carry "body": null for releases with no notes.
        var release = ReleaseParser.Parse(RealPayload);

        Assert.NotNull(release);
        Assert.Equal("", release.Notes);
    }

    [Fact]
    public void Parse_ReadsReleaseNotes()
    {
        var release = ReleaseParser.Parse("""
            {
              "tag_name": "v1.2.0",
              "name": "Version 1.2.0",
              "body": "## What's new\n- Auto-update",
              "html_url": "https://github.com/zeusapps/Invoicer/releases/tag/v1.2.0",
              "assets": [
                {
                  "name": "Invoicer-v1.2.0.zip",
                  "size": 17000000,
                  "browser_download_url": "https://example.invalid/Invoicer-v1.2.0.zip"
                }
              ]
            }
            """);

        Assert.NotNull(release);
        Assert.Equal("Version 1.2.0", release.Name);
        Assert.Equal("## What's new\n- Auto-update", release.Notes);
    }

    [Fact]
    public void Parse_NoAssets_ReturnsReleaseWithoutAsset()
    {
        var release = ReleaseParser.Parse("""
            {"tag_name": "v1.2.0", "assets": []}
            """);

        Assert.NotNull(release);
        Assert.Equal(new Version(1, 2, 0), release.Version);
        Assert.False(release.HasAsset);
    }

    [Fact]
    public void Parse_MissingOptionalFields_FallsBackToTagForName()
    {
        var release = ReleaseParser.Parse("""{"tag_name": "v1.2.0"}""");

        Assert.NotNull(release);
        Assert.Equal("v1.2.0", release.Name);
        Assert.Equal("", release.Notes);
        Assert.Equal("", release.ReleaseUrl);
        Assert.False(release.HasAsset);
    }

    [Fact]
    public void Parse_SingleUnexpectedlyNamedZip_IsAccepted()
    {
        var release = ReleaseParser.Parse("""
            {
              "tag_name": "v1.2.0",
              "assets": [
                {"name": "Invoicer-win-x64.zip", "size": 5, "browser_download_url": "https://example.invalid/a.zip"}
              ]
            }
            """);

        Assert.NotNull(release);
        Assert.Equal("https://example.invalid/a.zip", release.AssetUrl);
    }

    [Fact]
    public void Parse_AmbiguousZips_SelectsTheExpectedName()
    {
        var release = ReleaseParser.Parse("""
            {
              "tag_name": "v1.2.0",
              "assets": [
                {"name": "source.zip", "size": 1, "browser_download_url": "https://example.invalid/source.zip"},
                {"name": "Invoicer-v1.2.0.zip", "size": 2, "browser_download_url": "https://example.invalid/right.zip"}
              ]
            }
            """);

        Assert.NotNull(release);
        Assert.Equal("https://example.invalid/right.zip", release.AssetUrl);
        Assert.Equal(2, release.AssetSize);
    }

    [Fact]
    public void Parse_AmbiguousZipsWithoutExpectedName_SelectsNothing()
    {
        var release = ReleaseParser.Parse("""
            {
              "tag_name": "v1.2.0",
              "assets": [
                {"name": "one.zip", "size": 1, "browser_download_url": "https://example.invalid/one.zip"},
                {"name": "two.zip", "size": 2, "browser_download_url": "https://example.invalid/two.zip"}
              ]
            }
            """);

        Assert.NotNull(release);
        Assert.False(release.HasAsset);
    }

    [Fact]
    public void Parse_NonVersionTag_ParsesButHasNoVersion()
    {
        var release = ReleaseParser.Parse("""{"tag_name": "nightly"}""");

        Assert.NotNull(release);
        Assert.Null(release.Version);
        Assert.False(VersionComparer.IsNewer(release.Version, new Version(0, 0, 0)));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not json")]
    [InlineData("[]")]
    [InlineData("{}")]
    [InlineData("""{"tag_name": ""}""")]
    [InlineData("""{"message": "Not Found"}""")]
    public void Parse_UnusablePayload_ReturnsNull(string json)
    {
        Assert.Null(ReleaseParser.Parse(json));
    }

    [Theory]
    [InlineData(16951455, "16.2 MB")]
    [InlineData(524288, "512 KB")]
    [InlineData(0, "unknown size")]
    public void AssetSizeDisplay_IsHumanReadable(long size, string expected)
    {
        Assert.Equal(expected, new ReleaseInfo { AssetSize = size }.AssetSizeDisplay);
    }
}
