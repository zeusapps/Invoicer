namespace Invoicer.Update;

/// <summary>A published GitHub release and its downloadable executable asset.</summary>
public class ReleaseInfo
{
    /// <summary>Raw tag as published, e.g. "v1.2.0".</summary>
    public string Tag { get; init; } = "";

    /// <summary>Tag parsed to a comparable version, null when the tag is not semver-shaped.</summary>
    public Version? Version { get; init; }

    /// <summary>Release title; falls back to the tag when GitHub returns none.</summary>
    public string Name { get; init; } = "";

    /// <summary>Release notes, as Markdown.</summary>
    public string Notes { get; init; } = "";

    /// <summary>Release page on github.com.</summary>
    public string ReleaseUrl { get; init; } = "";

    /// <summary>Direct download URL for the Invoicer-&lt;tag&gt;.zip asset, empty when absent.</summary>
    public string AssetUrl { get; init; } = "";

    /// <summary>Asset size in bytes.</summary>
    public long AssetSize { get; init; }

    public bool HasAsset => !string.IsNullOrEmpty(AssetUrl);

    /// <summary>Asset size rendered for display, e.g. "42.3 MB".</summary>
    public string AssetSizeDisplay => AssetSize <= 0
        ? "unknown size"
        : AssetSize >= 1024 * 1024
            ? FormattableString.Invariant($"{AssetSize / (1024.0 * 1024.0):F1} MB")
            : FormattableString.Invariant($"{AssetSize / 1024.0:F0} KB");
}
