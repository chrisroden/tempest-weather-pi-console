using System.IO;
using System.Runtime.InteropServices;
using Tempest.UI.Services;
using Xunit;

namespace Tempest.UI.Tests;

public sealed class InstallUpdateRunnerTests
{
    [Fact]
    public void StripAnsi_RemovesColorCodes()
    {
        var stripped = InstallUpdateRunner.StripAnsi("\u001b[36m[INFO] hello\u001b[0m");
        Assert.Equal("[INFO] hello", stripped);
    }

    [Fact]
    public void CanApply_ReportsWhyWhenUnavailable()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux) && File.Exists(InstallUpdateRunner.HelperPath))
        {
            Assert.True(InstallUpdateRunner.CanApply(out var readyReason));
            Assert.Equal(string.Empty, readyReason);
            return;
        }

        Assert.False(InstallUpdateRunner.CanApply(out var reason));
        Assert.False(string.IsNullOrWhiteSpace(reason));
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            Assert.Contains("Raspberry Pi", reason);
        }
        else
        {
            Assert.Contains("Update helper is missing", reason);
        }
    }
}
