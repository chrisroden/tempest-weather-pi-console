namespace Tempest.UI.Services;

public sealed record UpdateCheckResult(
    bool UpdateAvailable,
    string InstalledVersion,
    string LatestVersion,
    string Message);

/// <summary>
/// Compares the installed VERSION file value to a GitHub release tag.
/// </summary>
public static class UpdateChecker
{
    public static UpdateCheckResult Compare(string? installedVersion, string? latestVersion)
    {
        var installed = Normalize(installedVersion);
        var latest = (latestVersion ?? string.Empty).Trim();
        if (string.IsNullOrEmpty(latest))
        {
            return new UpdateCheckResult(
                UpdateAvailable: false,
                InstalledVersion: installed,
                LatestVersion: string.Empty,
                Message: "Could not determine the latest release.");
        }

        if (string.Equals(installed, latest, System.StringComparison.Ordinal))
        {
            return new UpdateCheckResult(
                UpdateAvailable: false,
                InstalledVersion: installed,
                LatestVersion: latest,
                Message: $"You're on the latest version ({installed}).");
        }

        return new UpdateCheckResult(
            UpdateAvailable: true,
            InstalledVersion: installed,
            LatestVersion: latest,
            Message: $"Update available: {installed} → {latest}.");
    }

    private static string Normalize(string? installedVersion)
    {
        var value = (installedVersion ?? string.Empty).Trim();
        return string.IsNullOrEmpty(value) ? "unknown" : value;
    }
}
