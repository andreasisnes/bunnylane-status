namespace BunnyLane.Status.Jellyfin;

/// <param name="RemoteEndPoint">Bare client IP, port stripped. Used only to join with Traefik access-log lines.</param>
public sealed record Session(string UserId, string DisplayName, string RemoteEndPoint, bool IsPlaying, long BitrateBps, bool IsTranscoding);
