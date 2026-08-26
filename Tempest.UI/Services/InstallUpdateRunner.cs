using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace Tempest.UI.Services;

/// <summary>
/// Applies a GitHub release update via the root-owned helper written by install-pi.sh.
/// </summary>
public sealed class InstallUpdateRunner
{
    public const string HelperPath = "/usr/local/sbin/tempest-update";

    private static readonly Regex AnsiEscape = new(@"\x1B\[[0-9;]*m", RegexOptions.Compiled);

    public static bool CanApply(out string reason)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            reason = "Updates apply only on a Raspberry Pi install.";
            return false;
        }

        if (!File.Exists(HelperPath))
        {
            reason = "Update helper is missing. Run sudo /opt/tempest/install-pi.sh --update --yes once from a terminal to enable in-app updates.";
            return false;
        }

        reason = string.Empty;
        return true;
    }

    public async Task<int> ApplyAsync(Action<string> onLine, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(onLine);

        if (!CanApply(out var reason))
        {
            onLine(reason);
            return 1;
        }

        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "sudo",
                Arguments = $"-n {HelperPath}",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            },
            EnableRaisingEvents = true
        };

        void Append(string? line)
        {
            if (line is null)
            {
                return;
            }

            onLine(StripAnsi(line));
        }

        process.OutputDataReceived += (_, e) => Append(e.Data);
        process.ErrorDataReceived += (_, e) => Append(e.Data);

        try
        {
            if (!process.Start())
            {
                onLine("Failed to start the update helper.");
                return 1;
            }
        }
        catch (Exception ex)
        {
            onLine($"Failed to start the update helper: {ex.Message}");
            return 1;
        }

        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        try
        {
            await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            TryKill(process);
            throw;
        }

        return process.ExitCode;
    }

    public static string StripAnsi(string value)
    {
        return AnsiEscape.Replace(value, string.Empty);
    }

    private static void TryKill(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch (InvalidOperationException)
        {
            // Already exited.
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] update helper kill failed: {ex.Message}");
        }
    }
}
