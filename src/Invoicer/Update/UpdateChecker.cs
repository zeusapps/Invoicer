using System.Net;

namespace Invoicer.Update;

/// <summary>Asks GitHub for the latest stable release of the configured repository.</summary>
public static class UpdateChecker
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

    /// <summary>
    /// The releases/latest endpoint returns the newest published release that is neither a
    /// draft nor a pre-release, so pre-releases are excluded without any client-side filtering.
    /// </summary>
    public static async Task<UpdateCheckResult> CheckAsync(
        string repository,
        Version currentVersion,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(repository))
            return UpdateCheckResult.Failed("No update repository is configured.");

        try
        {
            using var client = CreateClient();
            using var response = await client.GetAsync(
                $"https://api.github.com/repos/{repository}/releases/latest",
                HttpCompletionOption.ResponseContentRead,
                cancellationToken);

            if (!response.IsSuccessStatusCode)
                return UpdateCheckResult.Failed(DescribeFailure(response.StatusCode));

            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            var release = ReleaseParser.Parse(json);

            if (release is null)
                return UpdateCheckResult.Failed("GitHub returned a release that could not be read.");

            // An unparseable tag is "nothing to update to", not an error worth showing.
            return VersionComparer.IsNewer(release.Version, currentVersion)
                ? UpdateCheckResult.Available(release)
                : UpdateCheckResult.UpToDate(release);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return UpdateCheckResult.Failed("The update check timed out.");
        }
        catch (HttpRequestException ex)
        {
            return UpdateCheckResult.Failed($"Could not reach GitHub: {ex.Message}");
        }
        catch (Exception ex)
        {
            return UpdateCheckResult.Failed(ex.Message);
        }
    }

    internal static HttpClient CreateClient()
    {
        var client = new HttpClient { Timeout = Timeout };

        // GitHub rejects API requests without a User-Agent with 403.
        client.DefaultRequestHeaders.Add("User-Agent", $"Invoicer/{AppVersion.Display}");
        client.DefaultRequestHeaders.Add("Accept", "application/vnd.github+json");
        // Pinned so a future default-version bump cannot reshape the payload under a shipped binary.
        client.DefaultRequestHeaders.Add("X-GitHub-Api-Version", "2022-11-28");

        return client;
    }

    private static string DescribeFailure(HttpStatusCode status) => status switch
    {
        HttpStatusCode.NotFound => "No releases were found for this repository.",
        HttpStatusCode.Forbidden or HttpStatusCode.TooManyRequests =>
            "GitHub rate limit reached. Try again later.",
        _ => $"GitHub returned {(int)status} {status}.",
    };
}
