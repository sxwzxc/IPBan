using DigitalRuby.IPBanCore;
using DigitalRuby.IPBanWebUI;
using Microsoft.AspNetCore.Mvc;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorPages();

// Register IPBan database service
builder.Services.AddSingleton<IPBanDatabaseService>();

// Read config path from appsettings or use default
var configPath = builder.Configuration["IPBan:ConfigPath"] ?? IPBanConfig.DefaultFileName;
var dbPath = builder.Configuration["IPBan:DatabasePath"] ?? IPBanDB.FileName;
builder.Services.AddSingleton(new IPBanWebSettings { ConfigPath = configPath, DatabasePath = dbPath });

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
}

app.UseStaticFiles();
app.UseRouting();
app.MapRazorPages();

// API endpoints
app.MapGet("/api/stats", (IPBanDatabaseService dbService) =>
{
    return Results.Json(dbService.GetStats());
});

app.MapGet("/api/banned", ([FromQuery] int page, [FromQuery] int pageSize, IPBanDatabaseService dbService) =>
{
    page = Math.Max(1, page);
    pageSize = Math.Clamp(pageSize, 10, 200);
    return Results.Json(dbService.GetBannedIPs(page, pageSize));
});

app.MapGet("/api/failed", ([FromQuery] int page, [FromQuery] int pageSize, IPBanDatabaseService dbService) =>
{
    page = Math.Max(1, page);
    pageSize = Math.Clamp(pageSize, 10, 200);
    return Results.Json(dbService.GetFailedLoginIPs(page, pageSize));
});

app.MapPost("/api/unban", async ([FromBody] UnbanRequest req, IPBanDatabaseService dbService) =>
{
    if (string.IsNullOrWhiteSpace(req?.IPAddress))
        return Results.BadRequest("IP address is required.");
    bool result = dbService.DeleteIP(req.IPAddress);
    return result ? Results.Ok(new { success = true }) : Results.NotFound(new { success = false, message = "IP not found." });
});

app.MapGet("/api/config", (IPBanWebSettings settings) =>
{
    try
    {
        string xml = File.Exists(settings.ConfigPath) ? File.ReadAllText(settings.ConfigPath) : "";
        return Results.Text(xml, "application/xml");
    }
    catch (Exception ex)
    {
        return Results.Problem(ex.Message);
    }
});

app.MapPost("/api/config", async (HttpContext context, IPBanWebSettings settings) =>
{
    try
    {
        using var reader = new StreamReader(context.Request.Body);
        string xml = await reader.ReadToEndAsync();
        // Basic XML validation
        var doc = new System.Xml.XmlDocument();
        doc.LoadXml(xml);
        await File.WriteAllTextAsync(settings.ConfigPath, xml);
        return Results.Ok(new { success = true });
    }
    catch (Exception ex)
    {
        return Results.BadRequest(new { success = false, message = ex.Message });
    }
});

app.Run();
