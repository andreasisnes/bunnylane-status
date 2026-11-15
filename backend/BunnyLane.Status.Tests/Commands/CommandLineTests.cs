using BunnyLane.Status.Commands;
using FluentAssertions;

namespace BunnyLane.Status.Tests.Commands;

public class CommandLineTests
{
    [Fact]
    public void Parses_a_command_with_an_allowed_argument()
    {
        var result = CommandLine.Parse("net 24h");

        result.Should().BeOfType<ParseResult.Ok>()
            .Which.Command.Should().BeEquivalentTo(new ParsedCommand("net", ["24h"]));
    }

    [Fact]
    public void Is_case_insensitive_and_trims_whitespace()
    {
        CommandLine.Parse("  NET  ").Should().BeOfType<ParseResult.Ok>()
            .Which.Command.Should().BeEquivalentTo(new ParsedCommand("net", []));
    }

    [Theory]
    [InlineData("net 2h")]
    [InlineData("status extra")]
    [InlineData("reboot")]
    [InlineData("top week")]
    public void Rejects_unknown_commands_and_disallowed_arguments(string line) =>
        CommandLine.Parse(line).Should().BeOfType<ParseResult.NotFound>();

    [Theory]
    [InlineData("net; rm -rf /")]
    [InlineData("net $(id)")]
    [InlineData("net && id")]
    [InlineData("help | cat")]
    public void Rejects_shell_metacharacters_and_echoes_the_raw_text(string line)
    {
        var result = CommandLine.Parse(line);

        result.Should().BeOfType<ParseResult.NotFound>().Which.Echoed.Should().Be(line);
    }

    [Fact]
    public void Watch_wraps_an_allowed_command()
    {
        CommandLine.Parse("watch net 1h").Should().BeOfType<ParseResult.Ok>()
            .Which.Command.Should().BeEquivalentTo(new ParsedCommand("watch", ["net", "1h"]));
    }

    [Theory]
    [InlineData("watch watch net")]
    [InlineData("watch")]
    [InlineData("watch net 2h")]
    [InlineData("watch help")]
    public void Watch_rejects_nesting_missing_or_invalid_targets(string line) =>
        CommandLine.Parse(line).Should().BeOfType<ParseResult.NotFound>();

    [Fact]
    public void Truncates_the_echo_of_an_over_long_line()
    {
        var result = CommandLine.Parse(new string('a', 300));

        result.Should().BeOfType<ParseResult.NotFound>().Which.Echoed.Should().HaveLength(80);
    }

    [Fact]
    public void Empty_line_is_not_found_with_empty_echo() =>
        CommandLine.Parse("").Should().BeOfType<ParseResult.NotFound>().Which.Echoed.Should().BeEmpty();

    [Fact]
    public void Allow_list_covers_every_spec_command()
    {
        CommandLine.Specs.Select(s => s.Name).Should().BeEquivalentTo(
            ["help", "status", "nodes", "net", "disks", "nas", "gpu", "vps", "top", "now", "watch"]);
        CommandLine.Specs.Single(s => s.Name == "net").Args.Should().BeEquivalentTo(["1h", "24h", "7d", "30d"]);
    }
}
