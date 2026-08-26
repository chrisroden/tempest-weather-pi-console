using System;
using System.IO;
using Tempest.UI;
using Xunit;

namespace Tempest.UI.Tests;

public sealed class InstallVersionTests
{
    [Fact]
    public void Read_UsesTempestInstallRootEnv()
    {
        var dir = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "tempest-version-" + Guid.NewGuid().ToString("N")));
        var previous = Environment.GetEnvironmentVariable("TEMPEST_INSTALL_ROOT");
        try
        {
            File.WriteAllText(Path.Combine(dir.FullName, "VERSION"), "v3.2.1\n");
            Environment.SetEnvironmentVariable("TEMPEST_INSTALL_ROOT", dir.FullName);

            Assert.Equal("v3.2.1", InstallVersion.Read());
        }
        finally
        {
            Environment.SetEnvironmentVariable("TEMPEST_INSTALL_ROOT", previous);
            dir.Delete(recursive: true);
        }
    }
}
