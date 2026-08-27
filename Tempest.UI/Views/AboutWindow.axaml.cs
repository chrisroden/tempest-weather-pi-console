using System;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Tempest.UI.Services;

namespace Tempest.UI.Views;

public partial class AboutWindow : Window
{
    private readonly HttpClient _httpClient;
    private readonly GitHubReleaseClient _releaseClient;
    private readonly InstallUpdateRunner _updateRunner;
    private readonly bool _ownsHttpClient;
    private bool _busy;
    private bool _applyAllowed;
    private string? _availableVersion;

    public AboutWindow()
        : this(InstallVersion.Read())
    {
    }

    public AboutWindow(string version)
        : this(version, releaseClient: null, updateRunner: null)
    {
    }

    public AboutWindow(string version, GitHubReleaseClient? releaseClient, InstallUpdateRunner? updateRunner)
    {
        InitializeComponent();
        VersionText.Text = string.IsNullOrWhiteSpace(version) ? "unknown" : version.Trim();

        if (releaseClient is null)
        {
            _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
            _releaseClient = new GitHubReleaseClient(_httpClient);
            _ownsHttpClient = true;
        }
        else
        {
            _httpClient = null!;
            _releaseClient = releaseClient;
            _ownsHttpClient = false;
        }

        _updateRunner = updateRunner ?? new InstallUpdateRunner();
        Opened += OnOpened;
        Closed += OnClosed;
    }

    private async void OnOpened(object? sender, EventArgs e)
    {
        Opened -= OnOpened;
        await CheckForUpdatesAsync();
    }

    private async Task CheckForUpdatesAsync()
    {
        if (_busy)
        {
            return;
        }

        _busy = true;
        SetButtonsEnabled(false);
        UpdateNowButton.IsVisible = false;
        RestartButton.IsVisible = false;
        _availableVersion = null;
        StatusText.Text = "Checking GitHub for the latest release...";

        try
        {
            var latest = await _releaseClient.GetLatestTagAsync();
            var result = UpdateChecker.Compare(VersionText.Text, latest);
            var helperReady = InstallUpdateRunner.CanApply(out var applyReason);
            var ui = AboutUpdateUi.AfterCheck(result, helperReady, applyReason);
            StatusText.Text = ui.Status;
            _availableVersion = result.UpdateAvailable ? result.LatestVersion : null;
            _applyAllowed = ui.EnableUpdateNow;
            UpdateNowButton.IsVisible = ui.ShowUpdateNow;
        }
        catch (Exception ex)
        {
            StatusText.Text = $"Could not check for updates: {ex.Message}";
            UpdateNowButton.IsVisible = false;
            _applyAllowed = false;
        }
        finally
        {
            _busy = false;
            SetButtonsEnabled(true);
        }
    }

    private async void OnUpdateNowClick(object? sender, RoutedEventArgs e)
    {
        if (_busy)
        {
            return;
        }

        if (!InstallUpdateRunner.CanApply(out var reason))
        {
            StatusText.Text = reason;
            return;
        }

        _busy = true;
        SetButtonsEnabled(false);
        UpdateNowButton.IsVisible = false;
        RestartButton.IsVisible = false;
        ShowUpdateLog();
        StatusText.Text = "Updating... this can take a few minutes.";
        AppendLog("Starting update...");

        try
        {
            var exitCode = await _updateRunner.ApplyAsync(line =>
            {
                Dispatcher.UIThread.Post(() => AppendLog(line));
            });

            if (exitCode == 0)
            {
                var version = _availableVersion ?? "the latest release";
                StatusText.Text = $"Update to {version} succeeded. Restart to load the new version.";
                AppendLog(StatusText.Text);
                RestartButton.IsVisible = true;
            }
            else
            {
                StatusText.Text = $"Update failed (exit {exitCode}). See the log for details.";
                AppendLog(StatusText.Text);
                UpdateNowButton.IsVisible = true;
            }
        }
        catch (Exception ex)
        {
            StatusText.Text = $"Update failed: {ex.Message}";
            AppendLog(StatusText.Text);
            UpdateNowButton.IsVisible = true;
        }
        finally
        {
            _busy = false;
            SetButtonsEnabled(true);
        }
    }

    private async void OnRestartClick(object? sender, RoutedEventArgs e)
    {
        if (_busy)
        {
            return;
        }

        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            StatusText.Text = "Restart is only supported on Linux (Raspberry Pi).";
            return;
        }

        _busy = true;
        SetButtonsEnabled(false);
        StatusText.Text = "Restarting backend service...";
        AppendLog(StatusText.Text);

        if (!await LinuxSudo.RunSystemctlAsync("restart", "tempest-backend.service"))
        {
            StatusText.Text = "Failed to restart backend (sudo/systemctl). Check /etc/sudoers.d/tempest.";
            AppendLog(StatusText.Text);
            _busy = false;
            SetButtonsEnabled(true);
            return;
        }

        StatusText.Text = "Restarting UI service...";
        AppendLog(StatusText.Text);
        await Task.Delay(300);

        if (!await LinuxSudo.RunSystemctlAsync("restart", "tempest-ui.service"))
        {
            AppendLog("UI restart via systemctl failed; exiting for systemd respawn...");
            Environment.Exit(0);
        }
    }

    private void OnCloseClick(object? sender, RoutedEventArgs e)
    {
        if (_busy)
        {
            return;
        }

        Close();
    }

    private void SetButtonsEnabled(bool enabled)
    {
        UpdateNowButton.IsEnabled = enabled && _applyAllowed;
        RestartButton.IsEnabled = enabled;
        CloseButton.IsEnabled = enabled;
    }

    private void ShowUpdateLog()
    {
        if (UpdateLogPanel.IsVisible)
        {
            return;
        }

        UpdateLogPanel.IsVisible = true;
    }

    private void AppendLog(string line)
    {
        if (string.IsNullOrWhiteSpace(line) || !UpdateLogPanel.IsVisible)
        {
            return;
        }

        if (string.IsNullOrEmpty(UpdateLog.Text))
        {
            UpdateLog.Text = line;
        }
        else
        {
            UpdateLog.Text += Environment.NewLine + line;
        }

        Dispatcher.UIThread.Post(() =>
        {
            UpdateLogScroller.Offset = new Vector(0, UpdateLogScroller.Extent.Height);
        }, DispatcherPriority.Background);
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        if (_ownsHttpClient)
        {
            _httpClient.Dispose();
        }
    }
}
