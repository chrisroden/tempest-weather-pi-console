using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Tempest.UI.Services;
using Xunit;

namespace Tempest.UI.Tests;

public sealed class GitHubReleaseClientTests
{
    [Fact]
    public void ParseTagName_ReadsTag()
    {
        var tag = GitHubReleaseClient.ParseTagName("""{"tag_name":"v4.1.0","name":"4.1.0"}""");
        Assert.Equal("v4.1.0", tag);
    }

    [Fact]
    public void ParseTagName_MissingTag_Throws()
    {
        Assert.Throws<InvalidOperationException>(() => GitHubReleaseClient.ParseTagName("""{"name":"nope"}"""));
    }

    [Fact]
    public void ParseTagName_EmptyTag_Throws()
    {
        Assert.Throws<InvalidOperationException>(() => GitHubReleaseClient.ParseTagName("""{"tag_name":"  "}"""));
    }

    [Fact]
    public async Task GetLatestTagAsync_SendsUserAgentAndReturnsTag()
    {
        HttpRequestMessage? captured = null;
        using var handler = new StubHandler(request =>
        {
            captured = request;
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""{"tag_name":"v9.9.9"}""")
            };
        });
        using var http = new HttpClient(handler);
        var client = new GitHubReleaseClient(http);

        var tag = await client.GetLatestTagAsync();

        Assert.Equal("v9.9.9", tag);
        Assert.NotNull(captured);
        Assert.Equal("TempestWeatherPiConsole", captured!.Headers.UserAgent.ToString());
        Assert.Contains("repos/chrisroden/tempest-weather-pi-console/releases/latest", captured.RequestUri?.ToString());
    }

    [Fact]
    public async Task GetLatestTagAsync_NonSuccess_Throws()
    {
        using var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.NotFound)
        {
            Content = new StringContent("""{"message":"Not Found"}""")
        });
        using var http = new HttpClient(handler);
        var client = new GitHubReleaseClient(http);

        await Assert.ThrowsAsync<HttpRequestException>(() => client.GetLatestTagAsync());
    }

    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _respond;

        public StubHandler(Func<HttpRequestMessage, HttpResponseMessage> respond)
        {
            _respond = respond;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(_respond(request));
        }
    }
}
