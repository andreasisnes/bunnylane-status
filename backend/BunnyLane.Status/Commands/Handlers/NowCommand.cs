using BunnyLane.Status.Jellyfin;
using BunnyLane.Status.Render;

namespace BunnyLane.Status.Commands.Handlers;

/// <summary>What is playing right now, as counts only. Never a name, never a title.</summary>
public sealed class NowCommand(IJellyfin jellyfin) : ICommandHandler
{
    public string Name => "now";

    public async Task<CommandResult> RunAsync(IReadOnlyList<string> args, CancellationToken ct)
    {
        IReadOnlyList<Session> sessions;
        try
        {
            sessions = await jellyfin.GetSessionsAsync(ct);
        }
        catch (JellyfinUnavailableException)
        {
            return CommandResult.Unknown(Name, new TagsBlock([new Tag("streams", Level.Unknown)]), new TextBlock("media server not answering", Tone.Muted));
        }

        var playing = sessions.Where(s => s.IsPlaying).ToList();
        var bitrate = playing.Sum(s => s.BitrateBps);
        var gpu = playing.Any(s => s.IsTranscoding);
        var streams = playing.Count == 1 ? "1 stream" : $"{playing.Count} streams";

        return CommandResult.Ok(Name,
            new TextBlock($"{streams} · {Format.Bits(bitrate)} · {(gpu ? "gpu in use" : "gpu idle")}", Tone.Amber),
            new TagsBlock([
                new Tag(streams, Level.Ok),
                new Tag(gpu ? "gpu in use" : "gpu idle", Level.Ok),
            ]));
    }
}
