using System.Text.Json;
using System.Text.Json.Serialization;

namespace BunnyLane.Status.Render;

/// <summary>Enums go over the wire as their lower-case names ("ok", "degraded").</summary>
public sealed class LowerCaseEnumConverter<T> : JsonStringEnumConverter<T> where T : struct, Enum
{
    public LowerCaseEnumConverter() : base(JsonNamingPolicy.CamelCase) { }
}

[JsonConverter(typeof(LowerCaseEnumConverter<Tone>))]
public enum Tone { Plain, Muted, Amber, Moon, Red }

[JsonConverter(typeof(LowerCaseEnumConverter<Level>))]
public enum Level { Ok, Degraded, Down, Unknown }

[JsonConverter(typeof(LowerCaseEnumConverter<ResultStatus>))]
public enum ResultStatus { Ok, Error, Unknown }

/// <summary>The closed set of things the terminal knows how to draw. The frontend renders these and nothing else.</summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(TextBlock), "text")]
[JsonDerivedType(typeof(TagsBlock), "tags")]
[JsonDerivedType(typeof(TableBlock), "table")]
[JsonDerivedType(typeof(BarBlock), "bar")]
[JsonDerivedType(typeof(SparklineBlock), "sparkline")]
[JsonDerivedType(typeof(ChartBlock), "chart")]
public abstract record Block;

public sealed record TextBlock(string Text, Tone Tone = Tone.Plain) : Block;

public sealed record Tag(string Label, Level Level);

public sealed record TagsBlock(IReadOnlyList<Tag> Tags) : Block;

public sealed record TableBlock(string[] Columns, string[][] Rows) : Block;

/// <param name="Fraction">0..1, already clamped by the producer.</param>
public sealed record BarBlock(string Label, double Fraction, string Caption) : Block;

public sealed record SparklineBlock(string Label, double[] Values, string Unit) : Block;

public sealed record Series(string Name, double?[] Values);

/// <param name="Timestamps">Unix seconds, one per sample; every series has the same length.</param>
public sealed record ChartBlock(string Title, string Unit, long[] Timestamps, IReadOnlyList<Series> Series) : Block;

public sealed record CommandResult(string Command, IReadOnlyList<Block> Blocks, ResultStatus Status)
{
    public static CommandResult Ok(string command, params Block[] blocks) => new(command, blocks, ResultStatus.Ok);
    public static CommandResult Unknown(string command, params Block[] blocks) => new(command, blocks, ResultStatus.Unknown);
    public static CommandResult Error(string command, string message) => new(command, [new TextBlock(message, Tone.Red)], ResultStatus.Error);
}
