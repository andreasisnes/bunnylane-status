namespace BunnyLane.Status.Jellyfin;

public sealed class JellyfinUnavailableException(string message, Exception? inner = null) : Exception(message, inner);

public interface IJellyfin
{
    Task<IReadOnlyList<Session>> GetSessionsAsync(CancellationToken ct);
}
