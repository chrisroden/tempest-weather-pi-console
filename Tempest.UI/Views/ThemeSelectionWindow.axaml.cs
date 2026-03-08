using System.Collections.Generic;
using System.Linq;
using System;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;

namespace Tempest.UI.Views;

public partial class ThemeSelectionWindow : Window
{
    private bool _isInitializing = true;
    private bool _isClosing;

    public ThemeSelectionWindow()
    {
        InitializeComponent();
        ThemeListBox.SelectionChanged += OnThemeSelectionChanged;
        _isInitializing = false;
    }

    public ThemeSelectionWindow(IEnumerable<string> themeNames, string selectedTheme)
        : this()
    {
        _isInitializing = true;

        var themes = themeNames.ToList();
        ThemeListBox.ItemsSource = themes;

        if (themes.Contains(selectedTheme))
        {
            ThemeListBox.SelectedItem = selectedTheme;
        }
        else if (themes.Count > 0)
        {
            ThemeListBox.SelectedIndex = 0;
        }

        _isInitializing = false;
    }

    private void OnThemeSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_isInitializing || _isClosing)
        {
            return;
        }

        if (ThemeListBox.SelectedItem is string selectedTheme && !string.IsNullOrWhiteSpace(selectedTheme))
        {
            _isClosing = true;
            DispatcherTimer.RunOnce(() => Close(selectedTheme), TimeSpan.FromMilliseconds(120));
        }
    }
}