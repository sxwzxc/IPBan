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

// Quick settings keys exposed in the structured settings UI
string[] QuickSettingsKeys =
[
    "FailedLoginAttemptsBeforeBan",
    "BanTime",
    "ExpireTime",
    "CycleTime",
    "ResetFailedLoginCountForUnbannedIPAddresses",
    "ClearBannedIPAddressesOnRestart",
    "ClearFailedLoginsOnSuccessfulLogin",
    "ProcessInternalIPAddresses",
    "Whitelist",
    "WhitelistRegex",
    "Blacklist",
    "BlacklistRegex",
    "FailedLoginAttemptsBeforeBanUserNameWhitelist",
    "UserNameWhitelist",
    "FirewallRulePrefix"
];

app.MapGet("/api/config/settings", (IPBanWebSettings settings) =>
{
    try
    {
        if (!File.Exists(settings.ConfigPath))
            return Results.Ok(new Dictionary<string, string>());

        var doc = new System.Xml.XmlDocument();
        doc.Load(settings.ConfigPath);
        var appSettings = doc.SelectSingleNode("//appSettings");
        var result = new Dictionary<string, string>();
        if (appSettings != null)
        {
            foreach (var key in QuickSettingsKeys)
            {
                // Find by iterating child nodes to avoid XPath string interpolation
                foreach (System.Xml.XmlNode child in appSettings.ChildNodes)
                {
                    if (child.Attributes?["key"]?.Value == key &&
                        child.Attributes?["value"] is { } attr)
                    {
                        result[key] = attr.Value;
                        break;
                    }
                }
            }
        }
        return Results.Json(result);
    }
    catch (Exception ex)
    {
        return Results.Problem(ex.Message);
    }
});

app.MapPost("/api/config/settings", async (HttpContext context, IPBanWebSettings settings) =>
{
    try
    {
        using var reader = new StreamReader(context.Request.Body);
        string body = await reader.ReadToEndAsync();
        var updates = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(body,
            new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        if (updates == null || updates.Count == 0)
            return Results.BadRequest(new { success = false, message = "No settings provided." });

        if (!File.Exists(settings.ConfigPath))
            return Results.NotFound(new { success = false, message = "Config file not found." });

        var doc = new System.Xml.XmlDocument();
        doc.Load(settings.ConfigPath);
        var appSettings = doc.SelectSingleNode("//appSettings");

        foreach (var kvp in updates)
        {
            // Only allow updating known quick-settings keys for safety
            if (!QuickSettingsKeys.Contains(kvp.Key)) continue;
            // Find by iterating child nodes to avoid XPath string interpolation
            if (appSettings != null)
            {
                foreach (System.Xml.XmlNode child in appSettings.ChildNodes)
                {
                    if (child.Attributes?["key"]?.Value == kvp.Key &&
                        child.Attributes?["value"] is { } attr)
                    {
                        attr.Value = kvp.Value ?? string.Empty;
                        break;
                    }
                }
            }
        }

        doc.Save(settings.ConfigPath);
        return Results.Ok(new { success = true });
    }
    catch (Exception ex)
    {
        return Results.BadRequest(new { success = false, message = ex.Message });
    }
});

app.Run();
