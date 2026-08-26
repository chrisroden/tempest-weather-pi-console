using Avalonia.Controls;
using Avalonia.Interactivity;

namespace Tempest.UI.Views;

public partial class AboutWindow : Window
{
    public AboutWindow()
        : this(InstallVersion.Read())
    {
    }

    public AboutWindow(string version)
    {
        InitializeComponent();
        VersionText.Text = string.IsNullOrWhiteSpace(version) ? "unknown" : version.Trim();
    }

    private void OnCloseClick(object? sender, RoutedEventArgs e)
    {
        Close();
    }
}
