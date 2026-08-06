namespace Invoicer.Models;

public class UpdateConfig
{
    /// <summary>Check GitHub for a newer release in the background at startup.</summary>
    public bool CheckOnStartup { get; set; } = true;

    /// <summary>GitHub repository to check, in "owner/name" form.</summary>
    public string Repository { get; set; } = "zeusapps/Invoicer";

    /// <summary>Version the user chose to skip; the startup check stays quiet until something newer appears.</summary>
    public string DismissedVersion { get; set; } = "";
}
