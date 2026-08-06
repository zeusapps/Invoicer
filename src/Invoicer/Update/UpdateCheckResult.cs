namespace Invoicer.Update;

public enum UpdateCheckStatus
{
    /// <summary>The running version is at or ahead of the latest release.</summary>
    UpToDate,

    /// <summary>A newer release exists.</summary>
    UpdateAvailable,

    /// <summary>The check could not be completed. Never conflate this with UpToDate.</summary>
    Failed,
}

public class UpdateCheckResult
{
    public UpdateCheckStatus Status { get; init; }

    /// <summary>The latest release, present when the check succeeded.</summary>
    public ReleaseInfo? Release { get; init; }

    /// <summary>Why the check failed, for display on a manual check.</summary>
    public string FailureReason { get; init; } = "";

    public static UpdateCheckResult UpToDate(ReleaseInfo? release = null) =>
        new() { Status = UpdateCheckStatus.UpToDate, Release = release };

    public static UpdateCheckResult Available(ReleaseInfo release) =>
        new() { Status = UpdateCheckStatus.UpdateAvailable, Release = release };

    public static UpdateCheckResult Failed(string reason) =>
        new() { Status = UpdateCheckStatus.Failed, FailureReason = reason };
}
