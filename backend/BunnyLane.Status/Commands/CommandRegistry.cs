namespace BunnyLane.Status.Commands;

public sealed class CommandRegistry(IEnumerable<ICommandHandler> handlers)
{
    private readonly Dictionary<string, ICommandHandler> _handlers = handlers.ToDictionary(h => h.Name);

    public ICommandHandler Resolve(ParsedCommand command) =>
        _handlers.TryGetValue(command.Name, out var handler)
            ? handler
            : throw new KeyNotFoundException($"no handler registered for '{command.Name}'");
}
