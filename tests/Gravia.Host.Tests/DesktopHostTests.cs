using System.Net;
using System.Net.WebSockets;
using Gravia.Host.Desktop;

namespace Gravia.Host.Tests;

public sealed class DesktopHostTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task RpcAcceptsOwnUiButRejectsOtherWebsites(bool sameOrigin)
    {
        await using var app = DesktopHost.Build(["--Desktop:Enabled=false"]);
        await app.StartAsync();
        using var socket = new ClientWebSocket();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        try
        {
            var address = new Uri(app.Urls.Single());
            socket.Options.SetRequestHeader("Origin", sameOrigin
                ? address.GetLeftPart(UriPartial.Authority) : "https://example.com");
            var endpoint = new UriBuilder(address) { Scheme = "ws", Path = "/rpc/ws", Query = "f=json5np" }.Uri;
            if (sameOrigin)
            {
                await socket.ConnectAsync(endpoint, timeout.Token);
                Assert.Equal(WebSocketState.Open, socket.State);
            }
            else
                await Assert.ThrowsAsync<WebSocketException>(() => socket.ConnectAsync(endpoint, timeout.Token));
        }
        finally
        {
            socket.Abort();
            await app.StopAsync();
        }
    }

    [Fact]
    public async Task ServesOnlyBlankShellOnDynamicLoopbackPort()
    {
        await using var app = DesktopHost.Build(["--Desktop:Enabled=false", "--urls=http://0.0.0.0:9999"]);
        await app.StartAsync();
        try
        {
            var address = new Uri(Assert.Single(app.Urls));
            Assert.Equal("127.0.0.1", address.Host);
            Assert.NotEqual(0, address.Port);
            Assert.NotEqual(9999, address.Port);
            using var client = new HttpClient { BaseAddress = address, Timeout = TimeSpan.FromSeconds(5) };
            using var response = await client.GetAsync("/");
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal("text/html", response.Content.Headers.ContentType?.MediaType);
            var html = await response.Content.ReadAsStringAsync();
            Assert.Contains("<div id=\"root\"></div>", html);
            Assert.Contains("default-src 'none'", response.Headers.GetValues("Content-Security-Policy").Single());
            var assets = System.Text.RegularExpressions.Regex.Matches(html, "(?:src|href)=\"(/assets/[^\"]+)\"");
            Assert.True(assets.Count >= 2, "The page must reference compiled JavaScript and CSS.");
            foreach (System.Text.RegularExpressions.Match asset in assets)
                Assert.Equal(HttpStatusCode.OK, (await client.GetAsync(asset.Groups[1].Value)).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/package.json")).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/appsettings.json")).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/shell.cjs")).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/renderer/main.tsx")).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/assets/missing.js")).StatusCode);
        }
        finally { await app.StopAsync(); }
    }

    [Fact]
    public void MissingConfiguredExecutableFailsClearly()
    {
        var options = new DesktopOptions { ExecutablePath = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".exe") };
        Assert.Throws<FileNotFoundException>(() => options.ResolveExecutable());
    }
}
