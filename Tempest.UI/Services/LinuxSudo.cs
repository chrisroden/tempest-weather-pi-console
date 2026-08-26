using System;
using System.Diagnostics;
using System.Threading.Tasks;

namespace Tempest.UI.Services;

/// <summary>
/// Passwordless sudo helpers for UI actions. Requires /etc/sudoers.d/tempest.
/// </summary>
public static class LinuxSudo
{
    public static async Task<bool> RunSystemctlAsync(string verb, string unit)
    {
        try
        {
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "sudo",
                    Arguments = $"-n /usr/bin/systemctl {verb} {unit}",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };
            process.Start();
            var stdout = await process.StandardOutput.ReadToEndAsync();
            var stderr = await process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();
            if (process.ExitCode != 0)
            {
                Console.WriteLine(
                    $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] systemctl {verb} {unit} exit={process.ExitCode} stderr={stderr.Trim()} stdout={stdout.Trim()}");
                return false;
            }

            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] systemctl {verb} {unit} exception: {ex.Message}");
            return false;
        }
    }
}
