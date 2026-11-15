using BunnyLane.Status.Commands;
using BunnyLane.Status.Render;

namespace BunnyLane.Status.Api;

/// <summary>Parse, resolve, run. The only path from visitor text to a handler.</summary>
public sealed class CommandRunner(CommandRegistry registry, ILogger<CommandRunner> log)
{
    public static ParseResult Parse(string line) => CommandLine.Parse(line);

    public static CommandResult NotFound(string echoed) =>
        CommandResult.Error(echoed, $"command not found: {echoed} · try help");

    /// <summary><c>watch x</c> submitted as a one-shot runs <c>x</c> once; the frontend opens the stream itself.</summary>
    public Task<CommandResult> RunAsync(ParsedCommand command, CancellationToken ct) =>
        command.Name == "watch"
            ? RunAsync(new ParsedCommand(command.Args[0], command.Args.Skip(1).ToList()), ct)
            : RunHandlerAsync(command, ct);

    private async Task<CommandResult> RunHandlerAsync(ParsedCommand command, CancellationToken ct)
    {
        try
        {
            return await registry.Resolve(command).RunAsync(command.Args, ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception e)
        {
            log.LogError(e, "command {Command} failed", command.Name);
            return CommandResult.Unknown(command.Name, new TextBlock("something went wrong on our side", Tone.Muted));
        }
    }
}
