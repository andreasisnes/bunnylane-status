using BunnyLane.Status.Jellyfin;
using FluentAssertions;
using Microsoft.Extensions.Options;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;

namespace BunnyLane.Status.Tests.Jellyfin;

public sealed class JellyfinClientTests : IDisposable
{
    private readonly WireMockServer _server = WireMockServer.Start();

    /// <summary>Trimmed capture of GET /Sessions: one transcode, one direct play, one idle session.</summary>
    public const string SessionsBody = """
        [
          {"Id":"a","UserId":"u1","UserName":"anna","RemoteEndPoint":"84.212.10.5",
           "NowPlayingItem":{"Name":"Secret Movie","MediaSources":[{"Bitrate":12000000}]},
           "TranscodingInfo":{"Bitrate":8000000,"IsVideoDirect":false},
           "PlayState":{"IsPaused":false}},
          {"Id":"b","UserId":"u2","UserName":"bo","RemoteEndPoint":"192.168.30.50:51234",
           "NowPlayingItem":{"Name":"Secret Show","MediaSources":[{"Bitrate":30000000}]},
           "PlayState":{"IsPaused":false}},
          {"Id":"c","UserId":"u3","UserName":"carl","RemoteEndPoint":"10.0.0.9","PlayState":{}}
        ]
        """;

    private JellyfinClient Client()
    {
        var options = new StatusOptions { JellyfinUrl = _server.Url!, JellyfinApiKey = "secret-key" };
        return new JellyfinClient(new HttpClient { BaseAddress = new Uri(options.JellyfinUrl) }, Options.Create(options));
    }

    [Fact]
    public async Task Parses_sessions_with_bitrate_from_transcode_or_media_source()
    {
        _server.Given(Request.Create().WithPath("/Sessions").WithParam("activeWithinSeconds", "120").UsingGet()
                .WithHeader("Authorization", "MediaBrowser Token=\"secret-key\""))
            .RespondWith(Response.Create().WithStatusCode(200).WithBody(SessionsBody));

        var sessions = await Client().GetSessionsAsync(CancellationToken.None);

        sessions.Should().HaveCount(3);
        var playing = sessions.Where(s => s.IsPlaying).ToList();
        playing.Should().HaveCount(2);
        playing[0].Should().Be(new Session("u1", "anna", "84.212.10.5", true, 8_000_000, true));
        playing[1].Should().Be(new Session("u2", "bo", "192.168.30.50", true, 30_000_000, false));
        sessions[2].IsPlaying.Should().BeFalse();
    }

    [Fact]
    public async Task Unauthorised_raises_JellyfinUnavailableException()
    {
        _server.Given(Request.Create().WithPath("/Sessions").UsingGet()).RespondWith(Response.Create().WithStatusCode(401));

        await Client().Invoking(c => c.GetSessionsAsync(CancellationToken.None)).Should().ThrowAsync<JellyfinUnavailableException>();
    }

    public void Dispose() => _server.Stop();
}
