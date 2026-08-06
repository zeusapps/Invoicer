using System.Reflection;

namespace Invoicer.Update;

/// <summary>
/// The version of the running executable, stamped by CI from the release tag.
/// Non-release builds report 0.0.0 so they never compare as newer than a real release.
/// </summary>
public static class AppVersion
{
    private static string? _display;
    private static Version? _current;

    /// <summary>Version as text, e.g. "1.2.0".</summary>
    public static string Display => _display ??= ReadDisplay();

    /// <summary>Version for comparison against a release tag.</summary>
    public static Version Current => _current ??=
        Version.TryParse(Display, out var parsed) ? parsed : new Version(0, 0, 0);

    private static string ReadDisplay()
    {
        var assembly = typeof(AppVersion).Assembly;

        var informational = assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion;

        if (!string.IsNullOrWhiteSpace(informational))
        {
            // Strip SourceLink's "+<commit sha>" build metadata suffix.
            var plus = informational.IndexOf('+');
            var trimmed = plus >= 0 ? informational[..plus] : informational;
            if (trimmed.Length > 0)
                return trimmed;
        }

        return assembly.GetName().Version?.ToString(3) ?? "0.0.0";
    }
}
