using Tempest.UI.Services;
using Xunit;

namespace Tempest.UI.Tests;

public sealed class AboutUpdateUiTests
{
    [Fact]
    public void AfterCheck_Latest_HidesUpdateNow()
    {
        var result = UpdateChecker.Compare("v1.2.3", "v1.2.3");

        var ui = AboutUpdateUi.AfterCheck(result, helperReady: true, helperReason: null);

        Assert.False(ui.ShowUpdateNow);
        Assert.False(ui.EnableUpdateNow);
        Assert.Equal("You're on the latest version (v1.2.3).", ui.Status);
    }

    [Fact]
    public void AfterCheck_UpdateAvailable_ShowsUpdateNow()
    {
        var result = UpdateChecker.Compare("v1.1.0", "v1.1.1");

        var ui = AboutUpdateUi.AfterCheck(result, helperReady: true, helperReason: string.Empty);

        Assert.True(ui.ShowUpdateNow);
        Assert.True(ui.EnableUpdateNow);
        Assert.Equal("Update available: v1.1.0 → v1.1.1.", ui.Status);
    }

    [Fact]
    public void AfterCheck_UpdateAvailableWithoutHelper_ShowsDisabledUpdateNow()
    {
        var result = UpdateChecker.Compare("v1.0.0", "v2.0.0");
        const string reason = "Update helper is missing.";

        var ui = AboutUpdateUi.AfterCheck(result, helperReady: false, helperReason: reason);

        Assert.True(ui.ShowUpdateNow);
        Assert.False(ui.EnableUpdateNow);
        Assert.Contains("Update available: v1.0.0 → v2.0.0.", ui.Status);
        Assert.Contains(reason, ui.Status);
    }
}
