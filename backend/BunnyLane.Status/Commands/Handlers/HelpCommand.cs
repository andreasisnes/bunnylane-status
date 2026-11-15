using BunnyLane.Status.Render;

namespace BunnyLane.Status.Commands.Handlers;

public sealed class HelpCommand : ICommandHandler
{
    public string Name => "help";

    public Task<CommandResult> RunAsync(IReadOnlyList<string> args, CancellationToken ct)
    {
        var rows = CommandLine.Specs.Select(s => new[] { s.Usage, s.Description }).ToArray();
        return Task.FromResult(CommandResult.Ok(Name, new TableBlock(["command", "output"], rows)));
    }
}
