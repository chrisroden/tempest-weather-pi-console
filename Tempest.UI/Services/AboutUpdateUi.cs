namespace Tempest.UI.Services;

public sealed record AboutUpdateUiState(
    bool ShowUpdateNow,
    bool EnableUpdateNow,
    string Status);

/// <summary>
/// Maps an update check to About dialog controls. The progress log is never shown from a check.
/// </summary>
public static class AboutUpdateUi
{
    public static AboutUpdateUiState AfterCheck(UpdateCheckResult result, bool helperReady, string? helperReason)
    {
        if (!result.UpdateAvailable)
        {
            return new AboutUpdateUiState(
                ShowUpdateNow: false,
                EnableUpdateNow: false,
                Status: result.Message);
        }

        var status = helperReady || string.IsNullOrWhiteSpace(helperReason)
            ? result.Message
            : $"{result.Message} {helperReason}";

        return new AboutUpdateUiState(
            ShowUpdateNow: true,
            EnableUpdateNow: helperReady,
            Status: status);
    }
}
