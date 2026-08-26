using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Tempest.UI.Services;

/// <summary>
/// Reads the latest GitHub release tag for this repository.
/// </summary>
public sealed class GitHubReleaseClient
{
    public const string DefaultRepository = "chrisroden/tempest-weather-pi-console";
    public const string UserAgent = "TempestWeatherPiConsole";

    private readonly HttpClient _httpClient;
    private readonly string _latestReleaseUrl;

    public GitHubReleaseClient(HttpClient httpClient, string? repository = null)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        var repo = string.IsNullOrWhiteSpace(repository) ? DefaultRepository : repository.Trim();
        _latestReleaseUrl = $"https://api.github.com/repos/{repo}/releases/latest";
    }

    public async Task<string> GetLatestTagAsync(CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, _latestReleaseUrl);
        request.Headers.TryAddWithoutValidation("User-Agent", UserAgent);
        request.Headers.TryAddWithoutValidation("Accept", "application/vnd.github+json");

        using var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException(
                $"GitHub latest-release request failed ({(int)response.StatusCode}): {TrimForError(body)}");
        }

        return ParseTagName(body);
    }

    public static string ParseTagName(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            throw new InvalidOperationException("GitHub release response was empty.");
        }

        using var document = JsonDocument.Parse(json);
        if (!document.RootElement.TryGetProperty("tag_name", out var tagElement))
        {
            throw new InvalidOperationException("GitHub release response did not include tag_name.");
        }

        var tag = tagElement.GetString();
        if (string.IsNullOrWhiteSpace(tag))
        {
            throw new InvalidOperationException("GitHub release tag_name was empty.");
        }

        return tag.Trim();
    }

    private static string TrimForError(string body)
    {
        var trimmed = body.Trim();
        return trimmed.Length <= 180 ? trimmed : trimmed[..180] + "...";
    }
}
