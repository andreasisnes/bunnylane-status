using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace BunnyLane.Status.Jellyfin;

public sealed class JellyfinClient(HttpClient http, IOptions<StatusOptions> options) : IJellyfin
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

    public async Task<IReadOnlyList<Session>> GetSessionsAsync(CancellationToken ct)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(Timeout);
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, "/Sessions?activeWithinSeconds=120");
            request.Headers.TryAddWithoutValidation("Authorization", $"MediaBrowser Token=\"{options.Value.JellyfinApiKey}\"");
            using var response = await http.SendAsync(request, timeout.Token);
            if (!response.IsSuccessStatusCode) throw new JellyfinUnavailableException($"jellyfin returned {(int)response.StatusCode}");
            using var doc = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(timeout.Token), cancellationToken: timeout.Token);
            return doc.RootElement.EnumerateArray().Select(Parse).ToList();
        }
        catch (OperationCanceledException e) when (!ct.IsCancellationRequested)
        {
            throw new JellyfinUnavailableException("jellyfin timed out", e);
        }
        catch (Exception e) when (e is HttpRequestException or JsonException or KeyNotFoundException or InvalidOperationException)
        {
            throw new JellyfinUnavailableException("jellyfin unreachable or malformed", e);
        }
    }

    private static Session Parse(JsonElement s)
    {
        var playing = s.TryGetProperty("NowPlayingItem", out var item) && item.ValueKind == JsonValueKind.Object;
        var transcoding = s.TryGetProperty("TranscodingInfo", out var tc) && tc.ValueKind == JsonValueKind.Object;
        long bitrate = 0;
        if (transcoding && tc.TryGetProperty("Bitrate", out var tb) && tb.ValueKind == JsonValueKind.Number) bitrate = tb.GetInt64();
        else if (playing && item.TryGetProperty("MediaSources", out var sources) && sources.ValueKind == JsonValueKind.Array)
        {
            var first = sources.EnumerateArray().FirstOrDefault();
            if (first.ValueKind == JsonValueKind.Object && first.TryGetProperty("Bitrate", out var mb) && mb.ValueKind == JsonValueKind.Number) bitrate = mb.GetInt64();
        }
        return new Session(
            Str(s, "UserId"), Str(s, "UserName"), StripPort(Str(s, "RemoteEndPoint")),
            playing, playing ? bitrate : 0, playing && transcoding);
    }

    private static string Str(JsonElement e, string name) =>
        e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() ?? "" : "";

    /// <summary>"1.2.3.4:5678" → "1.2.3.4"; IPv6 and bare addresses pass through untouched.</summary>
    internal static string StripPort(string endpoint) =>
        IPEndPoint.TryParse(endpoint, out var ep) ? ep.Address.ToString() : endpoint;
}
