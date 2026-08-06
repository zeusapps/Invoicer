using System.Text.Json;

namespace Invoicer.Update;

/// <summary>
/// Reads a GitHub "releases/latest" payload.
/// Uses JsonDocument rather than JsonSerializer.Deserialize&lt;T&gt; on purpose: the app is
/// published with PublishTrimmed=true, which breaks reflection-based deserialization in
/// ways that only surface in the shipped binary.
/// </summary>
public static class ReleaseParser
{
    public static ReleaseInfo? Parse(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return null;

        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;

            if (root.ValueKind != JsonValueKind.Object)
                return null;

            var tag = GetString(root, "tag_name");
            if (string.IsNullOrWhiteSpace(tag))
                return null;

            var hasVersion = VersionComparer.TryParseTag(tag, out var version);
            var (assetUrl, assetSize) = FindAsset(root, tag);
            var name = GetString(root, "name");

            return new ReleaseInfo
            {
                Tag = tag,
                Version = hasVersion ? version : null,
                Name = string.IsNullOrWhiteSpace(name) ? tag : name,
                Notes = GetString(root, "body"),
                ReleaseUrl = GetString(root, "html_url"),
                AssetUrl = assetUrl,
                AssetSize = assetSize,
            };
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>
    /// Picks the published executable archive. Prefers the exact name the release workflow
    /// produces, and otherwise falls back to the only .zip on the release.
    /// </summary>
    private static (string Url, long Size) FindAsset(JsonElement root, string tag)
    {
        if (!root.TryGetProperty("assets", out var assets) || assets.ValueKind != JsonValueKind.Array)
            return ("", 0);

        var expected = $"Invoicer-{tag}.zip";
        var zips = new List<(string Url, long Size)>();

        foreach (var asset in assets.EnumerateArray())
        {
            if (asset.ValueKind != JsonValueKind.Object)
                continue;

            var url = GetString(asset, "browser_download_url");
            if (string.IsNullOrEmpty(url))
                continue;

            var size = asset.TryGetProperty("size", out var sizeElement)
                       && sizeElement.ValueKind == JsonValueKind.Number
                       && sizeElement.TryGetInt64(out var parsedSize)
                ? parsedSize
                : 0;

            var name = GetString(asset, "name");
            if (string.Equals(name, expected, StringComparison.OrdinalIgnoreCase))
                return (url, size);

            if (name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
                zips.Add((url, size));
        }

        return zips.Count == 1 ? zips[0] : ("", 0);
    }

    private static string GetString(JsonElement element, string property) =>
        element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? ""
            : "";
}
