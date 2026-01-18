using Microsoft.Extensions.Logging.Testing;
using Orchestrator.Commands.Utility.Snapshots;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;

namespace Orchestrator.Tests.Commands.Utility.Snapshots;

/// <summary>
/// Tests for <see cref="SnapshotClient"/> using WireMock for HTTP stubbing.
/// </summary>
public class SnapshotClientTests
{
    private WireMockServer _server = null!;
    private HttpClient _httpClient = null!;
    private SnapshotClient _client = null!;

    [Before(Test)]
    public void SetUp()
    {
        _server = WireMockServer.Start();
        _httpClient = new HttpClient { BaseAddress = new Uri(_server.Urls[0]) };
        _client = new SnapshotClient(_httpClient, new FakeLogger<SnapshotClient>());
    }

    [After(Test)]
    public void TearDown()
    {
        _server?.Stop();
        _server?.Dispose();
        _httpClient?.Dispose();
    }

    private void StubHtmlResponse(string path, string htmlContent)
    {
        _server
            .Given(Request.Create()
                .WithPath(path)
                .UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "text/html; charset=utf-8")
                .WithBody(htmlContent));
    }

    private void StubNotFound(string path)
    {
        _server
            .Given(Request.Create()
                .WithPath(path)
                .UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(404));
    }

    [Test]
    public async Task FetchLoginPageAsync_returns_html_content_on_success()
    {
        // Arrange
        var expectedHtml = "<html><body>Login Form</body></html>";
        StubHtmlResponse("/info/profil/login", expectedHtml);

        // Act
        var result = await _client.FetchLoginPageAsync();

        // Assert
        await Assert.That(result).IsEqualTo(expectedHtml);
    }

    [Test]
    public async Task FetchLoginPageAsync_returns_null_on_error()
    {
        // Arrange
        StubNotFound("/info/profil/login");

        // Act
        var result = await _client.FetchLoginPageAsync();

        // Assert
        await Assert.That(result).IsNull();
    }

    [Test]
    public async Task FetchStandingsPageAsync_returns_html_content_on_success()
    {
        // Arrange
        var expectedHtml = "<html><body>Tabellen</body></html>";
        StubHtmlResponse("/test-community/tabellen", expectedHtml);

        // Act
        var result = await _client.FetchStandingsPageAsync("test-community");

        // Assert
        await Assert.That(result).IsEqualTo(expectedHtml);
    }

    [Test]
    public async Task FetchStandingsPageAsync_returns_null_on_error()
    {
        // Arrange
        StubNotFound("/test-community/tabellen");

        // Act
        var result = await _client.FetchStandingsPageAsync("test-community");

        // Assert
        await Assert.That(result).IsNull();
    }

    [Test]
    public async Task FetchTippabgabePageAsync_returns_html_content_on_success()
    {
        // Arrange
        var expectedHtml = "<html><body>Tippabgabe</body></html>";
        StubHtmlResponse("/test-community/tippabgabe", expectedHtml);

        // Act
        var result = await _client.FetchTippabgabePageAsync("test-community");

        // Assert
        await Assert.That(result).IsEqualTo(expectedHtml);
    }

    [Test]
    public async Task FetchTippabgabePageAsync_returns_null_on_error()
    {
        // Arrange
        StubNotFound("/test-community/tippabgabe");

        // Act
        var result = await _client.FetchTippabgabePageAsync("test-community");

        // Assert
        await Assert.That(result).IsNull();
    }

    [Test]
    public async Task FetchBonusPageAsync_requests_bonus_true_parameter()
    {
        // Arrange
        var expectedHtml = "<html><body>Bonus Questions</body></html>";
        _server
            .Given(Request.Create()
                .WithPath("/test-community/tippabgabe")
                .WithParam("bonus", "true")
                .UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "text/html; charset=utf-8")
                .WithBody(expectedHtml));

        // Act
        var result = await _client.FetchBonusPageAsync("test-community");

        // Assert
        await Assert.That(result).IsEqualTo(expectedHtml);
    }

    [Test]
    public async Task FetchBonusPageAsync_returns_null_on_error()
    {
        // Arrange
        _server
            .Given(Request.Create()
                .WithPath("/test-community/tippabgabe")
                .WithParam("bonus", "true")
                .UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(404));

        // Act
        var result = await _client.FetchBonusPageAsync("test-community");

        // Assert
        await Assert.That(result).IsNull();
    }

    [Test]
    public async Task FetchAllSpielinfoAsync_returns_empty_list_when_tippabgabe_fails()
    {
        // Arrange
        StubNotFound("/test-community/tippabgabe");

        // Act
        var result = await _client.FetchAllSpielinfoAsync("test-community");

        // Assert
        await Assert.That(result).IsEmpty();
    }

    [Test]
    public async Task FetchAllSpielinfoAsync_returns_empty_list_when_no_spielinfo_link_found()
    {
        // Arrange - tippabgabe page without spielinfo link
        var tippabgabeHtml = "<html><body>No spielinfo link here</body></html>";
        StubHtmlResponse("/test-community/tippabgabe", tippabgabeHtml);

        // Act
        var result = await _client.FetchAllSpielinfoAsync("test-community");

        // Assert
        await Assert.That(result).IsEmpty();
    }

    [Test]
    public async Task FetchAllSpielinfoAsync_fetches_single_spielinfo_page()
    {
        // Arrange - tippabgabe page with spielinfo link
        var tippabgabeHtml = """
            <html><body>
                <a href="/test-community/spielinfo/123">Tippabgabe mit Spielinfos</a>
            </body></html>
            """;
        StubHtmlResponse("/test-community/tippabgabe", tippabgabeHtml);

        // Spielinfo page without next link (disabled)
        var spielinfoHtml = """
            <html><body>
                <div class="spielinfo">Match Info</div>
                <div class="prevnextNext disabled"><a href="">No more</a></div>
            </body></html>
            """;
        StubHtmlResponse("/test-community/spielinfo/123", spielinfoHtml);

        // Act
        var result = await _client.FetchAllSpielinfoAsync("test-community");

        // Assert
        await Assert.That(result).HasCount().EqualTo(1);
        await Assert.That(result[0].fileName).IsEqualTo("spielinfo-01");
        await Assert.That(result[0].content).Contains("Match Info");
    }

    [Test]
    public async Task FetchAllSpielinfoAsync_traverses_multiple_spielinfo_pages()
    {
        // Arrange - tippabgabe page with spielinfo link
        var tippabgabeHtml = """
            <html><body>
                <a href="/test-community/spielinfo/1">Spielinfos</a>
            </body></html>
            """;
        StubHtmlResponse("/test-community/tippabgabe", tippabgabeHtml);

        // First spielinfo page with next link
        var spielinfo1Html = """
            <html><body>
                <div class="spielinfo">Match 1</div>
                <div class="prevnextNext"><a href="/test-community/spielinfo/2">Next</a></div>
            </body></html>
            """;
        StubHtmlResponse("/test-community/spielinfo/1", spielinfo1Html);

        // Second spielinfo page with next link
        var spielinfo2Html = """
            <html><body>
                <div class="spielinfo">Match 2</div>
                <div class="prevnextNext"><a href="/test-community/spielinfo/3">Next</a></div>
            </body></html>
            """;
        StubHtmlResponse("/test-community/spielinfo/2", spielinfo2Html);

        // Third spielinfo page - no more matches (disabled)
        var spielinfo3Html = """
            <html><body>
                <div class="spielinfo">Match 3</div>
                <div class="prevnextNext disabled"><a href="">Next</a></div>
            </body></html>
            """;
        StubHtmlResponse("/test-community/spielinfo/3", spielinfo3Html);

        // Act
        var result = await _client.FetchAllSpielinfoAsync("test-community");

        // Assert
        await Assert.That(result).HasCount().EqualTo(3);
        await Assert.That(result[0].fileName).IsEqualTo("spielinfo-01");
        await Assert.That(result[1].fileName).IsEqualTo("spielinfo-02");
        await Assert.That(result[2].fileName).IsEqualTo("spielinfo-03");
    }

    [Test]
    public async Task FetchAllSpielinfoHomeAwayAsync_adds_ansicht_parameter_and_suffix()
    {
        // Arrange - tippabgabe page with spielinfo link
        var tippabgabeHtml = """
            <html><body>
                <a href="/test-community/spielinfo/1">Spielinfos</a>
            </body></html>
            """;
        StubHtmlResponse("/test-community/tippabgabe", tippabgabeHtml);

        // Spielinfo page with ansicht=2 parameter (disabled next = only one match)
        var spielinfoHtml = """
            <html><body>
                <div class="spielinfo">Home/Away Match</div>
                <div class="prevnextNext disabled"><a href="">Next</a></div>
            </body></html>
            """;
        _server
            .Given(Request.Create()
                .WithPath("/test-community/spielinfo/1")
                .WithParam("ansicht", "2")
                .UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "text/html; charset=utf-8")
                .WithBody(spielinfoHtml));

        // Act
        var result = await _client.FetchAllSpielinfoHomeAwayAsync("test-community");

        // Assert
        await Assert.That(result).HasCount().EqualTo(1);
        await Assert.That(result[0].fileName).IsEqualTo("spielinfo-01-homeaway");
    }

    [Test]
    public async Task FetchAllSpielinfoHeadToHeadAsync_adds_ansicht_parameter_and_suffix()
    {
        // Arrange - tippabgabe page with spielinfo link
        var tippabgabeHtml = """
            <html><body>
                <a href="/test-community/spielinfo/1">Spielinfos</a>
            </body></html>
            """;
        StubHtmlResponse("/test-community/tippabgabe", tippabgabeHtml);

        // Spielinfo page with ansicht=3 parameter (disabled next = only one match)
        var spielinfoHtml = """
            <html><body>
                <div class="spielinfo">H2H Match</div>
                <div class="prevnextNext disabled"><a href="">Next</a></div>
            </body></html>
            """;
        _server
            .Given(Request.Create()
                .WithPath("/test-community/spielinfo/1")
                .WithParam("ansicht", "3")
                .UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "text/html; charset=utf-8")
                .WithBody(spielinfoHtml));

        // Act
        var result = await _client.FetchAllSpielinfoHeadToHeadAsync("test-community");

        // Assert
        await Assert.That(result).HasCount().EqualTo(1);
        await Assert.That(result[0].fileName).IsEqualTo("spielinfo-01-h2h");
    }

    [Test]
    public async Task FetchAllSpielinfoAsync_stops_when_spielinfo_fetch_fails()
    {
        // Arrange - tippabgabe page with spielinfo link
        var tippabgabeHtml = """
            <html><body>
                <a href="/test-community/spielinfo/1">Spielinfos</a>
            </body></html>
            """;
        StubHtmlResponse("/test-community/tippabgabe", tippabgabeHtml);

        // First spielinfo page fails
        StubNotFound("/test-community/spielinfo/1");

        // Act
        var result = await _client.FetchAllSpielinfoAsync("test-community");

        // Assert
        await Assert.That(result).IsEmpty();
    }

    [Test]
    public async Task FetchAllSpielinfoAsync_handles_relative_spielinfo_url()
    {
        // Arrange - tippabgabe page with relative spielinfo link (starts with /)
        var tippabgabeHtml = """
            <html><body>
                <a href="/test-community/spielinfo/abc">Spielinfos</a>
            </body></html>
            """;
        StubHtmlResponse("/test-community/tippabgabe", tippabgabeHtml);

        var spielinfoHtml = """
            <html><body>
                <div class="spielinfo">Match</div>
                <div class="prevnextNext disabled"><a href="">Next</a></div>
            </body></html>
            """;
        StubHtmlResponse("/test-community/spielinfo/abc", spielinfoHtml);

        // Act
        var result = await _client.FetchAllSpielinfoAsync("test-community");

        // Assert
        await Assert.That(result).HasCount().EqualTo(1);
    }

    [Test]
    public async Task FetchAllSpielinfoAsync_stops_when_no_next_button_found()
    {
        // Arrange - tippabgabe page with spielinfo link
        var tippabgabeHtml = """
            <html><body>
                <a href="/test-community/spielinfo/1">Spielinfos</a>
            </body></html>
            """;
        StubHtmlResponse("/test-community/tippabgabe", tippabgabeHtml);

        // Spielinfo page without any prevnextNext element
        var spielinfoHtml = """
            <html><body>
                <div class="spielinfo">Single Match Only</div>
            </body></html>
            """;
        StubHtmlResponse("/test-community/spielinfo/1", spielinfoHtml);

        // Act
        var result = await _client.FetchAllSpielinfoAsync("test-community");

        // Assert
        await Assert.That(result).HasCount().EqualTo(1);
    }

    [Test]
    public async Task FetchAllSpielinfoAsync_returns_empty_list_when_spielinfo_link_has_empty_href()
    {
        // Arrange - tippabgabe page with spielinfo link that has empty href
        var tippabgabeHtml = """
            <html><body>
                <a href="">Spielinfo with empty href</a>
            </body></html>
            """;
        StubHtmlResponse("/test-community/tippabgabe", tippabgabeHtml);

        // Act
        var result = await _client.FetchAllSpielinfoAsync("test-community");

        // Assert
        await Assert.That(result).IsEmpty();
    }

    [Test]
    public async Task FetchAllSpielinfoAsync_stops_when_next_button_has_empty_href()
    {
        // Arrange - tippabgabe page with spielinfo link
        var tippabgabeHtml = """
            <html><body>
                <a href="/test-community/spielinfo/1">Spielinfos</a>
            </body></html>
            """;
        StubHtmlResponse("/test-community/tippabgabe", tippabgabeHtml);

        // Spielinfo page with next button that has empty href
        var spielinfoHtml = """
            <html><body>
                <div class="spielinfo">Match</div>
                <div class="prevnextNext"><a href="">Empty Next</a></div>
            </body></html>
            """;
        StubHtmlResponse("/test-community/spielinfo/1", spielinfoHtml);

        // Act
        var result = await _client.FetchAllSpielinfoAsync("test-community");

        // Assert - Should fetch only the first page since next has empty href
        await Assert.That(result).HasCount().EqualTo(1);
    }
}
