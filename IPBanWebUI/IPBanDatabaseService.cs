using DigitalRuby.IPBanCore;

namespace DigitalRuby.IPBanWebUI;

/// <summary>
/// Settings for the IPBan Web UI
/// </summary>
public class IPBanWebSettings
{
    /// <summary>Config file path</summary>
    public string ConfigPath { get; set; } = IPBanConfig.DefaultFileName;

    /// <summary>Database file path</summary>
    public string DatabasePath { get; set; } = IPBanDB.FileName;
}

/// <summary>
/// Request model for unbanning an IP address
/// </summary>
public class UnbanRequest
{
    /// <summary>IP address to unban</summary>
    public string? IPAddress { get; set; }
}

/// <summary>
/// Dashboard statistics
/// </summary>
public class IPBanStats
{
    /// <summary>Total IP addresses in the database</summary>
    public int TotalIPs { get; set; }

    /// <summary>Currently banned IP addresses</summary>
    public int BannedIPs { get; set; }

    /// <summary>IP addresses with failed logins but not yet banned</summary>
    public int FailedLoginIPs { get; set; }
}

/// <summary>
/// Paginated result
/// </summary>
public class PagedResult<T>
{
    /// <summary>Items for this page</summary>
    public List<T> Items { get; set; } = [];

    /// <summary>Total number of items</summary>
    public int Total { get; set; }

    /// <summary>Current page (1-based)</summary>
    public int Page { get; set; }

    /// <summary>Page size</summary>
    public int PageSize { get; set; }
}

/// <summary>
/// Service for reading IPBan database information
/// </summary>
public class IPBanDatabaseService
{
    private readonly IPBanWebSettings _settings;

    public IPBanDatabaseService(IPBanWebSettings settings)
    {
        _settings = settings;
    }

    private IPBanDB OpenDB() => new(_settings.DatabasePath);

    /// <summary>Get dashboard statistics</summary>
    public IPBanStats GetStats()
    {
        if (!File.Exists(_settings.DatabasePath))
        {
            return new IPBanStats();
        }

        using var db = OpenDB();
        var all = db.EnumerateIPAddresses().ToList();
        int total = all.Count;
        int banned = all.Count(e => e.BanStartDate != null);
        int failedLogin = all.Count(e => e.BanStartDate == null);
        return new IPBanStats
        {
            TotalIPs = total,
            BannedIPs = banned,
            FailedLoginIPs = failedLogin
        };
    }

    /// <summary>Get banned IP addresses with pagination</summary>
    public PagedResult<IPBanDB.IPAddressEntry> GetBannedIPs(int page, int pageSize)
    {
        if (!File.Exists(_settings.DatabasePath))
        {
            return new PagedResult<IPBanDB.IPAddressEntry> { Page = page, PageSize = pageSize };
        }

        using var db = OpenDB();
        var allBanned = db.EnumerateIPAddresses()
            .Where(e => e.BanStartDate != null)
            .OrderByDescending(e => e.BanStartDate)
            .ToList();

        int total = allBanned.Count;
        var items = allBanned.Skip((page - 1) * pageSize).Take(pageSize).ToList();

        return new PagedResult<IPBanDB.IPAddressEntry>
        {
            Items = items,
            Total = total,
            Page = page,
            PageSize = pageSize
        };
    }

    /// <summary>Get IP addresses with failed logins (not yet banned) with pagination</summary>
    public PagedResult<IPBanDB.IPAddressEntry> GetFailedLoginIPs(int page, int pageSize)
    {
        if (!File.Exists(_settings.DatabasePath))
        {
            return new PagedResult<IPBanDB.IPAddressEntry> { Page = page, PageSize = pageSize };
        }

        using var db = OpenDB();
        var allFailed = db.EnumerateIPAddresses()
            .Where(e => e.BanStartDate == null)
            .OrderByDescending(e => e.LastFailedLogin)
            .ToList();

        int total = allFailed.Count;
        var items = allFailed.Skip((page - 1) * pageSize).Take(pageSize).ToList();

        return new PagedResult<IPBanDB.IPAddressEntry>
        {
            Items = items,
            Total = total,
            Page = page,
            PageSize = pageSize
        };
    }

    /// <summary>Delete an IP address from the database</summary>
    public bool DeleteIP(string ipAddress)
    {
        if (!File.Exists(_settings.DatabasePath))
        {
            return false;
        }

        using var db = OpenDB();
        return db.DeleteIPAddress(ipAddress);
    }
}
