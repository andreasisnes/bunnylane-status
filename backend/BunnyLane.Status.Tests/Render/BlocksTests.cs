using System.Text.Json;
using BunnyLane.Status.Render;
using FluentAssertions;

namespace BunnyLane.Status.Tests.Render;

public class BlocksTests
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public static IEnumerable<object[]> AllBlocks() =>
    [
        [new TextBlock("hello", Tone.Amber), "text"],
        [new TagsBlock([new Tag("nodes 6/6", Level.Ok), new Tag("2 workloads", Level.Degraded)]), "tags"],
        [new TableBlock(["#", "who"], [["1", "anna"], ["2", "bo"]]), "table"],
        [new BarBlock("vps", 0.385, "2.31 TB of 6 TB"), "bar"],
        [new SparklineBlock("GB/day", [1, 2, 3], "GB"), "sparkline"],
        [new ChartBlock("net 24h", "bit/s", [1700000000L, 1700000300L], [new Series("in", [1.0, null]), new Series("out", [2.0, 3.0])]), "chart"],
    ];

    [Theory]
    [MemberData(nameof(AllBlocks))]
    public void Every_block_serialises_with_its_type_discriminator_and_round_trips(Block block, string type)
    {
        var json = JsonSerializer.Serialize(block, Json);

        using var doc = JsonDocument.Parse(json);
        doc.RootElement.GetProperty("type").GetString().Should().Be(type);
        JsonSerializer.Deserialize<Block>(json, Json).Should().BeEquivalentTo(block, o => o.RespectingRuntimeTypes());
    }

    [Fact]
    public void CommandResult_serialises_blocks_polymorphically()
    {
        var result = new CommandResult("status", [new TextBlock("ok", Tone.Plain)], ResultStatus.Ok);

        var json = JsonSerializer.Serialize(result, Json);

        json.Should().Contain("\"type\":\"text\"").And.Contain("\"status\":\"ok\"").And.Contain("\"tone\":\"plain\"");
    }

    [Fact]
    public void Level_and_status_serialise_as_lower_case_strings()
    {
        JsonSerializer.Serialize(new Tag("x", Level.Unknown), Json).Should().Contain("\"level\":\"unknown\"");
        JsonSerializer.Serialize(new CommandResult("x", [], ResultStatus.Unknown), Json).Should().Contain("\"status\":\"unknown\"");
    }

    [Fact]
    public void A_bar_without_a_level_omits_the_property() =>
        JsonSerializer.Serialize<Block>(new BarBlock("nas", 0.39, "2.34 / 6.00 TB"), Json).Should().NotContain("\"level\"");

    [Fact]
    public void A_bar_with_a_level_serialises_it_lower_case() =>
        JsonSerializer.Serialize<Block>(new BarBlock("nas", 0.85, "x", Level.Down), Json).Should().Contain("\"level\":\"down\"");

    [Fact]
    public void A_table_without_levels_omits_the_property() =>
        JsonSerializer.Serialize<Block>(new TableBlock(["a"], [["1"]]), Json).Should().NotContain("\"levels\"");

    [Fact]
    public void A_table_with_levels_serialises_nulls_and_bands() =>
        JsonSerializer.Serialize<Block>(new TableBlock(["node", "cpu"], [["w-1", "63%"]], [[null, Level.Degraded]]), Json)
            .Should().Contain("\"levels\":[[null,\"degraded\"]]");
}
