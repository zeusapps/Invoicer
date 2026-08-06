namespace Invoicer.Update;

/// <summary>
/// Compares a GitHub release tag against the running version.
/// Only major/minor/patch participate: assembly versions carry a fourth revision
/// component that tags never do, and comparing it would make equal versions differ.
/// </summary>
public static class VersionComparer
{
    /// <summary>Parses "v1.2.0" or "1.2.0" into a version. Returns false for anything else.</summary>
    public static bool TryParseTag(string? tag, out Version version)
    {
        version = new Version(0, 0, 0);

        if (string.IsNullOrWhiteSpace(tag))
            return false;

        var trimmed = tag.Trim();
        if (trimmed.StartsWith('v') || trimmed.StartsWith('V'))
            trimmed = trimmed[1..];

        if (!Version.TryParse(trimmed, out var parsed))
            return false;

        // Version.TryParse accepts "1.2"; normalise the unspecified components to 0.
        version = new Version(parsed.Major, parsed.Minor, Math.Max(parsed.Build, 0));
        return true;
    }

    /// <summary>True when <paramref name="candidate"/> is a strictly newer release than <paramref name="current"/>.</summary>
    public static bool IsNewer(Version? candidate, Version current)
    {
        if (candidate is null)
            return false;

        return Truncate(candidate) > Truncate(current);
    }

    private static Version Truncate(Version version) =>
        new(version.Major, version.Minor, Math.Max(version.Build, 0));
}
