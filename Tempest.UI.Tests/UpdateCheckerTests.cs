using Tempest.UI.Services;
using Xunit;

namespace Tempest.UI.Tests;

public sealed class UpdateCheckerTests
{
    [Fact]
    public void Compare_SameTag_ReportsAlreadyLatest()
    {
        var result = UpdateChecker.Compare("v1.2.3", "v1.2.3");

        Assert.False(result.UpdateAvailable);
        Assert.Equal("v1.2.3", result.InstalledVersion);
        Assert.Equal("v1.2.3", result.LatestVersion);
        Assert.Equal("You're on the latest version (v1.2.3).", result.Message);
    }

    [Fact]
    public void Compare_DifferentTag_ReportsUpdateAvailable()
    {
        var result = UpdateChecker.Compare("v1.0.0", "v1.2.3");

        Assert.True(result.UpdateAvailable);
        Assert.Equal("Update available: v1.0.0 → v1.2.3.", result.Message);
    }

    [Fact]
    public void Compare_BlankInstalled_TreatsAsUnknown()
    {
        var result = UpdateChecker.Compare("  ", "v2.0.0");

        Assert.True(result.UpdateAvailable);
        Assert.Equal("unknown", result.InstalledVersion);
        Assert.Equal("Update available: unknown → v2.0.0.", result.Message);
    }

    [Fact]
    public void Compare_EmptyLatest_DoesNotOfferUpdate()
    {
        var result = UpdateChecker.Compare("v1.0.0", " ");

        Assert.False(result.UpdateAvailable);
        Assert.Equal("Could not determine the latest release.", result.Message);
    }
}
