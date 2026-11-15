using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace BunnyLane.Status.Metrics;

public sealed class PrometheusClient(HttpClient http, IMemoryCache cache, IOptions<StatusOptions> options, PrometheusHealth health) : IPrometheus
{
    private readonly StatusOptions _options = options.Value;

    public Task<Instant[]> QueryAsync(string promql, CancellationToken ct) =>
        Cached($"q|{promql}", async token =>
        {
            using var doc = await GetAsync($"/api/v1/query?query={Uri.EscapeDataString(promql)}", token);
            return Result(doc).EnumerateArray().Select(r => new Instant(Labels(r), Number(r.GetProperty("value")[1]))).ToArray();
        }, ct);

    public Task<RangeSeries[]> QueryRangeAsync(string promql, DateTimeOffset from, DateTimeOffset to, TimeSpan step, CancellationToken ct)
    {
        var start = from.ToUnixTimeSeconds();
        var end = to.ToUnixTimeSeconds();
        var stepSeconds = Math.Max(1, (long)step.TotalSeconds);
        var grid = Grid(start, end, stepSeconds);
        return Cached($"r|{promql}|{start}|{end}|{stepSeconds}", async token =>
        {
            using var doc = await GetAsync(
                $"/api/v1/query_range?query={Uri.EscapeDataString(promql)}&start={start}&end={end}&step={stepSeconds}", token);
            return Result(doc).EnumerateArray().Select(r =>
            {
                var values = new double?[grid.Length];
                foreach (var sample in r.GetProperty("values").EnumerateArray())
                {
                    var ts = (long)Number(sample[0]);
                    var index = (ts - start) / stepSeconds;
                    if (index >= 0 && index < grid.Length && grid[index] == ts) values[index] = Number(sample[1]);
                }
                return new RangeSeries(Labels(r), grid, values);
            }).ToArray();
        }, ct);
    }

    private async Task<T> Cached<T>(string key, Func<CancellationToken, Task<T>> fetch, CancellationToken ct)
    {
        if (cache.TryGetValue(key, out T? hit) && hit is not null) return hit;
        var value = await fetch(ct);
        cache.Set(key, value, _options.PrometheusCache);
        return value;
    }

    private async Task<JsonDocument> GetAsync(string path, CancellationToken ct)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(_options.PrometheusTimeout);
        try
        {
            using var response = await http.GetAsync(path, timeout.Token);
            if (!response.IsSuccessStatusCode)
                throw new MetricsUnavailableException($"prometheus returned {(int)response.StatusCode}");
            var doc = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(timeout.Token), cancellationToken: timeout.Token);
            if (doc.RootElement.GetProperty("status").GetString() != "success")
                throw new MetricsUnavailableException("prometheus query failed");
            health.MarkSuccess();
            return doc;
        }
        catch (OperationCanceledException e) when (!ct.IsCancellationRequested)
        {
            throw new MetricsUnavailableException("prometheus timed out", e);
        }
        catch (Exception e) when (e is HttpRequestException or JsonException or KeyNotFoundException or InvalidOperationException)
        {
            throw new MetricsUnavailableException("prometheus unreachable or malformed", e);
        }
    }

    private static JsonElement Result(JsonDocument doc) => doc.RootElement.GetProperty("data").GetProperty("result");

    private static Dictionary<string, string> Labels(JsonElement r) =>
        r.GetProperty("metric").EnumerateObject().ToDictionary(p => p.Name, p => p.Value.GetString() ?? string.Empty);

    private static double Number(JsonElement e) => e.ValueKind == JsonValueKind.Number
        ? e.GetDouble()
        : double.Parse(e.GetString()!, NumberStyles.Float, CultureInfo.InvariantCulture);

    private static long[] Grid(long start, long end, long step)
    {
        var count = (int)((end - start) / step) + 1;
        var grid = new long[Math.Max(count, 0)];
        for (var i = 0; i < grid.Length; i++) grid[i] = start + i * step;
        return grid;
    }
}
