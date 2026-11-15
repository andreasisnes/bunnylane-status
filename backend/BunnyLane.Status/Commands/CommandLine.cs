using System.Text.RegularExpressions;

namespace BunnyLane.Status.Commands;

public sealed record ParsedCommand(string Name, IReadOnlyList<string> Args);

/// <param name="Args">The only accepted values for the single optional argument; empty means the command takes none.</param>
public sealed record CommandSpec(string Name, string Usage, string Description, string[] Args);

public abstract record ParseResult
{
    public sealed record Ok(ParsedCommand Command) : ParseResult;
    public sealed record NotFound(string Echoed) : ParseResult;
}

/// <summary>
/// Turns visitor input into an allow-listed command. Nothing that is not literally in <see cref="Specs"/>
/// gets through, so no visitor text ever reaches Prometheus, Jellyfin or anything else as-is.
/// </summary>
public static partial class CommandLine
{
    public const int MaxEcho = 80;
    private const int MaxTokens = 3;

    public static readonly IReadOnlyList<CommandSpec> Specs =
    [
        new("help", "help", "this table", []),
        new("status", "status", "one line per area: nodes, storage, workloads, edge", []),
        new("nodes", "nodes", "node pool: role, cpu, memory, load, uptime", []),
        new("net", "net [1h|24h|7d|30d]", "WAN in/out now, sparkline and a chart for the range", ["1h", "24h", "7d", "30d"]),
        new("disks", "disks", "storage per worker: used/free, volumes, replica health", []),
        new("nas", "nas", "media share: used/total, growth per day", []),
        new("gpu", "gpu [1h|24h]", "gpu busy, vram, temperature, transcodes now", ["1h", "24h"]),
        new("vps", "vps [month]", "this month's traffic against the 6 TB cap; 'month' lists the last 6", ["month"]),
        new("top", "top [month|all]", "leaderboard: watch time and vps data per person", ["month", "all"]),
        new("now", "now", "streams playing, total bitrate, gpu in use", []),
        new("watch", "watch <cmd>", "re-run <cmd> every 2 s until Ctrl+C or stop", []),
    ];

    private static readonly Dictionary<string, CommandSpec> ByName = Specs.ToDictionary(s => s.Name);

    public static ParseResult Parse(string line)
    {
        var raw = line ?? string.Empty;
        var echoed = raw.Length > MaxEcho ? raw[..MaxEcho] : raw;
        var tokens = raw.Trim().ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        if (tokens.Length is 0 or > MaxTokens || tokens.Any(t => !Token().IsMatch(t)))
            return new ParseResult.NotFound(echoed);

        if (tokens[0] == "watch")
        {
            if (tokens.Length == 1 || tokens[1] is "watch" or "help") return new ParseResult.NotFound(echoed);
            return Match(tokens[1..]) is { } inner
                ? new ParseResult.Ok(new ParsedCommand("watch", [inner.Name, .. inner.Args]))
                : new ParseResult.NotFound(echoed);
        }

        return Match(tokens) is { } cmd ? new ParseResult.Ok(cmd) : new ParseResult.NotFound(echoed);
    }

    private static ParsedCommand? Match(string[] tokens)
    {
        if (!ByName.TryGetValue(tokens[0], out var spec) || spec.Name == "watch") return null;
        return tokens.Length switch
        {
            1 => new ParsedCommand(spec.Name, []),
            2 when spec.Args.Contains(tokens[1]) => new ParsedCommand(spec.Name, [tokens[1]]),
            _ => null,
        };
    }

    [GeneratedRegex("^[a-z0-9]+$")]
    private static partial Regex Token();
}
