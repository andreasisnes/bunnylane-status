using BunnyLane.Status.Render;

namespace BunnyLane.Status.Commands;

public interface ICommandHandler
{
    string Name { get; }
    Task<CommandResult> RunAsync(IReadOnlyList<string> args, CancellationToken ct);
}
