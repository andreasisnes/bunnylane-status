using System.Text.Json;
using BunnyLane.Status.Commands;
using Microsoft.AspNetCore.RateLimiting;

namespace BunnyLane.Status.Api;

public static class CommandEndpoint
{
    public const int MaxBodyBytes = 256;

    public static IEndpointRouteBuilder MapCommandEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/command", async (HttpContext http, CommandRunner runner, CancellationToken ct) =>
        {
            if (http.Request.ContentLength is > MaxBodyBytes) return Results.StatusCode(StatusCodes.Status413PayloadTooLarge);

            string line;
            try
            {
                using var buffer = new MemoryStream();
                await http.Request.Body.CopyToAsync(buffer, ct);
                if (buffer.Length > MaxBodyBytes) return Results.StatusCode(StatusCodes.Status413PayloadTooLarge);
                buffer.Position = 0;
                using var doc = await JsonDocument.ParseAsync(buffer, cancellationToken: ct);
                line = doc.RootElement.TryGetProperty("line", out var l) && l.ValueKind == JsonValueKind.String ? l.GetString() ?? "" : "";
            }
            catch (JsonException)
            {
                return Results.BadRequest(new { error = "body must be {\"line\": string}" });
            }

            var result = CommandRunner.Parse(line) switch
            {
                ParseResult.Ok ok => await runner.RunAsync(ok.Command, ct),
                ParseResult.NotFound nf => CommandRunner.NotFound(nf.Echoed),
                _ => CommandRunner.NotFound(""),
            };
            return Results.Json(result, StatusJson.Options);
        }).RequireRateLimiting(RateLimit.Commands);
        return app;
    }
}

public static class StatusJson
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);
}
