var builder = WebApplication.CreateBuilder(new WebApplicationOptions { Args = args, WebRootPath = "wwwroot" });
var app = builder.Build();

app.UseDefaultFiles();
app.UseStaticFiles();

app.MapGet("/healthz", () => Results.Text("ok"));

app.Run();

public partial class Program;
