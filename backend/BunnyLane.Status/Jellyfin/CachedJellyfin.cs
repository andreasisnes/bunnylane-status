using Microsoft.Extensions.Caching.Memory;

namespace BunnyLane.Status.Jellyfin;

/// <summary>One Jellyfin round trip per window, shared by `now`, the ticker and the sampler.</summary>
public sealed class CachedJellyfin(IJellyfin inner, IMemoryCache cache) : IJellyfin
{
    public static readonly TimeSpan Window = TimeSpan.FromSeconds(5);
    private const string Key = "jellyfin|sessions";
    private readonly SemaphoreSlim _gate = new(1, 1);

    public async Task<IReadOnlyList<Session>> GetSessionsAsync(CancellationToken ct)
    {
        if (cache.TryGetValue(Key, out IReadOnlyList<Session>? hit) && hit is not null) return hit;
        await _gate.WaitAsync(ct);
        try
        {
            if (cache.TryGetValue(Key, out hit) && hit is not null) return hit;
            var sessions = await inner.GetSessionsAsync(ct);
            cache.Set(Key, sessions, Window);
            return sessions;
        }
        finally
        {
            _gate.Release();
        }
    }
}
