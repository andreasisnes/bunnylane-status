using BunnyLane.Status.Commands;
using BunnyLane.Status.Commands.Handlers;
using BunnyLane.Status.Render;
using FluentAssertions;

namespace BunnyLane.Status.Tests.Commands;

public class HelpCommandTests
{
    [Fact]
    public async Task Help_returns_a_table_with_one_row_per_command()
    {
        var result = await new HelpCommand().RunAsync([], CancellationToken.None);

        result.Status.Should().Be(ResultStatus.Ok);
        var table = result.Blocks.Should().ContainSingle().Which.Should().BeOfType<TableBlock>().Subject;
        table.Columns.Should().BeEquivalentTo(["command", "output"]);
        table.Rows.Select(r => r[0]).Should().Contain(["help", "status", "net [1h|24h|7d|30d]", "watch <cmd>"]);
    }

    [Fact]
    public void Registry_resolves_a_parsed_command_to_its_handler()
    {
        var registry = new CommandRegistry([new HelpCommand()]);

        registry.Resolve(new ParsedCommand("help", [])).Should().BeOfType<HelpCommand>();
        registry.Invoking(r => r.Resolve(new ParsedCommand("nope", []))).Should().Throw<KeyNotFoundException>();
    }
}
