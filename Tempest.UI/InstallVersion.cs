using System;
using System.IO;

namespace Tempest.UI;

/// <summary>
/// Resolves the installed package version written by install-pi.sh to INSTALL_ROOT/VERSION.
/// </summary>
public static class InstallVersion
{
    private const string DefaultInstallRoot = "/opt/tempest";

    public static string Read()
    {
        foreach (var path in CandidatePaths())
        {
            try
            {
                if (!File.Exists(path))
                {
                    continue;
                }

                var value = File.ReadAllText(path).Trim();
                if (!string.IsNullOrEmpty(value))
                {
                    return value;
                }
            }
            catch (IOException)
            {
                // Try the next candidate.
            }
            catch (UnauthorizedAccessException)
            {
                // Try the next candidate.
            }
        }

        return "unknown";
    }

    private static string[] CandidatePaths()
    {
        var envRoot = Environment.GetEnvironmentVariable("TEMPEST_INSTALL_ROOT");
        var relativeToUi = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "VERSION"));

        if (!string.IsNullOrWhiteSpace(envRoot))
        {
            return
            [
                Path.Combine(envRoot.Trim(), "VERSION"),
                relativeToUi,
                Path.Combine(DefaultInstallRoot, "VERSION"),
            ];
        }

        return
        [
            relativeToUi,
            Path.Combine(DefaultInstallRoot, "VERSION"),
        ];
    }
}
