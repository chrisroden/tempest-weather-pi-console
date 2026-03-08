using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace Tempest.UI;

public static class ThemeManager
{
    private const string ThemesResourceKey = "NamedThemes";
    private const string DefaultThemeName = "Default";
    private static readonly string[] KnownThemeNames = ["Default", "Walnut", "Snow", "White-Ash"];
    private static readonly HashSet<string> AppliedThemeKeys = new(StringComparer.Ordinal);

    public static string CurrentThemeName { get; private set; } = DefaultThemeName;

    public static IReadOnlyList<string> GetAvailableThemeNames()
    {
        var themesDictionary = GetThemesDictionary();
        if (themesDictionary is null)
        {
            return KnownThemeNames;
        }

        var discovered = themesDictionary
            .Where(pair => pair.Key is string && pair.Value is ResourceDictionary)
            .Select(pair => (string)pair.Key)
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (discovered.Length == 0)
        {
            return KnownThemeNames;
        }

        return discovered
            .Concat(KnownThemeNames)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public static void InitializeFromConfiguration()
    {
        var savedTheme = LoadSelectedThemeName();
        if (!ApplyTheme(savedTheme, persistSelection: false))
        {
            EnsureDefaultFallbackResources();
        }
    }

    public static bool ApplyTheme(string? requestedThemeName, bool persistSelection)
    {
        var app = Application.Current;
        var themesDictionary = GetThemesDictionary();

        if (app is null)
        {
            return false;
        }

        if (themesDictionary is null)
        {
            EnsureDefaultFallbackResources();
            return false;
        }

        var selectedThemeName = ResolveThemeName(themesDictionary, requestedThemeName);
        var themeDictionary = TryGetThemeDictionary(themesDictionary, selectedThemeName)
                              ?? TryGetThemeDictionary(themesDictionary, DefaultThemeName);

        if (themeDictionary is null)
        {
            EnsureDefaultFallbackResources();
            return false;
        }

        foreach (var key in AppliedThemeKeys.ToArray())
        {
            app.Resources.Remove(key);
        }

        AppliedThemeKeys.Clear();

        foreach (var pair in themeDictionary)
        {
            if (pair.Key is not string key)
            {
                continue;
            }

            if (string.Equals(key, "CardShadow", StringComparison.Ordinal) && pair.Value is string shadowValue)
            {
                app.Resources[key] = BoxShadows.Parse(shadowValue);
            }
            else
            {
                app.Resources[key] = pair.Value;
            }

            AppliedThemeKeys.Add(key);
        }

        CurrentThemeName = selectedThemeName;

        if (persistSelection)
        {
            SaveSelectedThemeName(selectedThemeName);
        }

        return true;
    }

    public static IBrush GetThemeBrush(string resourceKey, string fallbackHex)
    {
        if (Application.Current?.TryGetResource(resourceKey, Application.Current.ActualThemeVariant, out var value) == true)
        {
            if (value is IBrush brush)
            {
                return brush;
            }

            if (value is string brushString &&
                Color.TryParse(brushString, out var parsedColor))
            {
                return new SolidColorBrush(parsedColor);
            }
        }

        return new SolidColorBrush(Color.Parse(fallbackHex));
    }

    public static string GetThemeString(string resourceKey, string fallbackValue)
    {
        if (Application.Current?.TryGetResource(resourceKey, Application.Current.ActualThemeVariant, out var value) == true &&
            value is string stringValue &&
            !string.IsNullOrWhiteSpace(stringValue))
        {
            return stringValue;
        }

        return fallbackValue;
    }

    private static ResourceDictionary? GetThemesDictionary()
    {
        if (Application.Current?.TryGetResource(ThemesResourceKey, Application.Current.ActualThemeVariant, out var themesObject) == true &&
            themesObject is ResourceDictionary themesDictionary)
        {
            return themesDictionary;
        }

        return null;
    }

    private static void EnsureDefaultFallbackResources()
    {
        var app = Application.Current;
        if (app is null)
        {
            return;
        }

        app.Resources["WindowBackgroundBrush"] = new SolidColorBrush(Color.Parse("#0A1929"));
        app.Resources["CardBackgroundBrush"] = new SolidColorBrush(Color.Parse("#132F4C"));
        app.Resources["ForecastDayBackgroundBrush"] = new SolidColorBrush(Color.Parse("#0D2238"));
        app.Resources["TextPrimaryBrush"] = new SolidColorBrush(Color.Parse("#FFFFFF"));
        app.Resources["TextSecondaryBrush"] = new SolidColorBrush(Color.Parse("#B2BAC2"));
        app.Resources["AccentBrush"] = new SolidColorBrush(Color.Parse("#4ECDC4"));
        app.Resources["DangerBrush"] = new SolidColorBrush(Color.Parse("#FF6B6B"));
        app.Resources["WarningBrush"] = new SolidColorBrush(Color.Parse("#F7931E"));
        app.Resources["CompassMarkerFillBrush"] = new SolidColorBrush(Color.Parse("#FFD700"));
        app.Resources["CompassMarkerStrokeBrush"] = new SolidColorBrush(Color.Parse("#FFA500"));
        app.Resources["CardShadow"] = BoxShadows.Parse("0 4 16 0 #00000033");
        app.Resources["StatusInfoColor"] = "#F7931E";
        app.Resources["StatusErrorColor"] = "#DC3545";
        app.Resources["StatusConnectedBrush"] = new SolidColorBrush(Color.Parse("#4ECDC4"));
        app.Resources["StatusDisconnectedBrush"] = new SolidColorBrush(Color.Parse("#FF6B6B"));
        app.Resources["StatusUnknownBrush"] = new SolidColorBrush(Color.Parse("#666666"));
        CurrentThemeName = DefaultThemeName;
    }

    private static string ResolveThemeName(ResourceDictionary themesDictionary, string? requestedThemeName)
    {
        if (!string.IsNullOrWhiteSpace(requestedThemeName) &&
            TryGetThemeDictionary(themesDictionary, requestedThemeName) is not null)
        {
            return themesDictionary
                .Where(pair => pair.Key is string && pair.Value is ResourceDictionary)
                .Select(pair => (string)pair.Key)
                .First(name => string.Equals(name, requestedThemeName, StringComparison.OrdinalIgnoreCase));
        }

        return TryGetThemeDictionary(themesDictionary, DefaultThemeName) is not null
            ? DefaultThemeName
            : themesDictionary
                .Where(pair => pair.Key is string && pair.Value is ResourceDictionary)
                .Select(pair => (string)pair.Key)
                .FirstOrDefault() ?? DefaultThemeName;
    }

    private static ResourceDictionary? TryGetThemeDictionary(ResourceDictionary themesDictionary, string themeName)
    {
        if (themesDictionary.TryGetValue(themeName, out var directValue) &&
            directValue is ResourceDictionary directDictionary)
        {
            return directDictionary;
        }

        foreach (var pair in themesDictionary)
        {
            if (pair.Key is string key &&
                string.Equals(key, themeName, StringComparison.OrdinalIgnoreCase) &&
                pair.Value is ResourceDictionary dictionary)
            {
                return dictionary;
            }
        }

        return null;
    }

    private static string LoadSelectedThemeName()
    {
        try
        {
            var productionConfigPath = GetProductionConfigPath();
            if (File.Exists(productionConfigPath))
            {
                var productionContent = File.ReadAllText(productionConfigPath);
                var productionNode = JsonNode.Parse(productionContent) as JsonObject;
                var productionTheme = productionNode?["Ui"]?["SelectedTheme"]?.GetValue<string>();
                if (!string.IsNullOrWhiteSpace(productionTheme))
                {
                    return productionTheme;
                }
            }

            var defaultConfigPath = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
            if (File.Exists(defaultConfigPath))
            {
                var defaultContent = File.ReadAllText(defaultConfigPath);
                var defaultNode = JsonNode.Parse(defaultContent) as JsonObject;
                var defaultTheme = defaultNode?["Ui"]?["SelectedTheme"]?.GetValue<string>();
                if (!string.IsNullOrWhiteSpace(defaultTheme))
                {
                    return defaultTheme;
                }
            }
        }
        catch
        {
        }

        return DefaultThemeName;
    }

    private static void SaveSelectedThemeName(string selectedThemeName)
    {
        try
        {
            var productionConfigPath = GetProductionConfigPath();
            JsonObject root;

            if (File.Exists(productionConfigPath))
            {
                var content = File.ReadAllText(productionConfigPath);
                root = JsonNode.Parse(content) as JsonObject ?? new JsonObject();
            }
            else
            {
                root = new JsonObject();
            }

            var uiSection = root["Ui"] as JsonObject ?? new JsonObject();
            uiSection["SelectedTheme"] = selectedThemeName;
            root["Ui"] = uiSection;

            var json = root.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(productionConfigPath, json);
        }
        catch
        {
        }
    }

    private static string GetProductionConfigPath()
    {
        return Path.Combine(AppContext.BaseDirectory, "appsettings.Production.json");
    }
}