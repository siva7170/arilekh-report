using System.Collections.Concurrent;
using System.Data;
using ReportDesigner.Core;
using ReportDesigner.Core.Data;
using ReportDesigner.Core.Model;
using ReportDesigner.Core.Rendering;
using ReportDesigner.WebApi.DTOs;

namespace ReportDesigner.WebApi.Services;

// ── Server-side data provider interface ───────────────────────────────────────

/// <summary>
/// Implement this interface in your microservice to load data from a database
/// (or any source) server-side. Register it with a key at startup:
///
///   builder.Services.AddSingleton&lt;ReportSessionService&gt;();
///   app.Services.GetRequiredService&lt;ReportSessionService&gt;()
///      .RegisterServerDataProvider("SalesReport", new SalesReportDataProvider(connectionString));
/// </summary>
public interface IServerDataProvider : IDataSourceProvider
{
    /// <summary>Called before rendering so the provider can accept form parameters.</summary>
    void SetParameters(IReadOnlyDictionary<string, object?> parameters);
}

/// <summary>
/// Singleton service that renders reports and stores the resulting
/// <see cref="ReportDocument"/> in memory keyed by a GUID session ID.
/// </summary>
public sealed class ReportSessionService : IDisposable
{
    // ── Session store ──────────────────────────────────────────────────────────

    private sealed record Session(ReportDocument Document, DateTime ExpiresAt);

    private readonly ConcurrentDictionary<string, Session>              _sessions  = new();
    private readonly ConcurrentDictionary<string, IServerDataProvider>  _providers = new();
    private readonly Timer _evictionTimer;
    private readonly ILogger<ReportSessionService> _logger;

    public ReportSessionService(ILogger<ReportSessionService> logger)
    {
        _logger = logger;
        _evictionTimer = new Timer(Evict, null,
            TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(5));
    }

    // ── Server data provider registration ─────────────────────────────────────

    /// <summary>
    /// Register a server-side data provider by key.
    /// Call at startup: sessions.RegisterServerDataProvider("key", provider).
    /// </summary>
    public void RegisterServerDataProvider(string key, IServerDataProvider provider)
        => _providers[key] = provider;

    public IServerDataProvider? GetServerDataProvider(string key)
        => _providers.TryGetValue(key, out var p) ? p : null;

    // ── Render from Angular (JSON data) ───────────────────────────────────────

    public async Task<(string SessionId, ReportDocument Document, long RenderMs)>
        RenderAsync(
            string reportXml,
            Dictionary<string, List<Dictionary<string, object?>>> dataSources,
            Dictionary<string, object?> parameters,
            int ttlMinutes,
            CancellationToken ct = default)
    {
        var sw      = System.Diagnostics.Stopwatch.StartNew();
        var report  = XmlReportSerializer.LoadFromXml(reportXml);
        var provider = new DictionaryDataProvider(dataSources);
        var engine  = new ReportEngine();
        var doc     = await engine.RenderAsync(report, provider, parameters, ct);
        sw.Stop();
        return Store(doc, ttlMinutes, sw.ElapsedMilliseconds);
    }

    // ── Render server-side (IServerDataProvider loads from DB) ────────────────

    public async Task<(string SessionId, ReportDocument Document, long RenderMs)>
        RenderWithProviderAsync(
            string reportXml,
            IServerDataProvider provider,
            IReadOnlyDictionary<string, object?> parameters,
            int ttlMinutes,
            CancellationToken ct = default)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        provider.SetParameters(parameters);
        var report = XmlReportSerializer.LoadFromXml(reportXml);
        var engine = new ReportEngine();
        var doc    = await engine.RenderAsync(report, provider, parameters, ct);
        sw.Stop();
        return Store(doc, ttlMinutes, sw.ElapsedMilliseconds);
    }

    // ── Store a pre-rendered document (called from your own controller code) ──

    /// <summary>
    /// Store a <see cref="ReportDocument"/> rendered entirely within your own
    /// server code, returning a GUID that Angular can use to load pages.
    /// </summary>
    public string StoreDocument(ReportDocument doc, int ttlMinutes = 30)
    {
        var (id, _, _) = Store(doc, ttlMinutes, 0);
        return id;
    }

    // ── Retrieval ─────────────────────────────────────────────────────────────

    public ReportDocument? GetDocument(string sessionId)
    {
        if (_sessions.TryGetValue(sessionId, out var s) && s.ExpiresAt > DateTime.UtcNow)
            return s.Document;
        return null;
    }

    public RenderedPage? GetPage(string sessionId, int pageNumber)
    {
        var doc = GetDocument(sessionId);
        if (doc is null || pageNumber < 1 || pageNumber > doc.PageCount) return null;
        return doc.Pages[pageNumber - 1];
    }

    // ── Internal helpers ──────────────────────────────────────────────────────

    private (string SessionId, ReportDocument Document, long RenderMs) Store(
        ReportDocument doc, int ttlMinutes, long ms)
    {
        var id = Guid.NewGuid().ToString("N");
        _sessions[id] = new Session(doc, DateTime.UtcNow.AddMinutes(ttlMinutes));
        _logger.LogInformation("Report session {Id}: {Pages} pages in {Ms}ms", id, doc.PageCount, ms);
        return (id, doc, ms);
    }

    private void Evict(object? _)
    {
        var now     = DateTime.UtcNow;
        var expired = _sessions.Where(kv => kv.Value.ExpiresAt <= now).Select(kv => kv.Key).ToList();
        foreach (var key in expired) _sessions.TryRemove(key, out _);
        if (expired.Count > 0)
            _logger.LogInformation("Evicted {Count} report sessions.", expired.Count);
    }

    public void Dispose() => _evictionTimer.Dispose();
}

// ── DictionaryDataProvider ─────────────────────────────────────────────────

public sealed class DictionaryDataProvider : IDataSourceProvider
{
    private readonly Dictionary<string, DataTable> _tables = new(StringComparer.OrdinalIgnoreCase);

    public DictionaryDataProvider(Dictionary<string, List<Dictionary<string, object?>>> sources)
    {
        foreach (var (name, rows) in sources)
            _tables[name] = ToDataTable(name, rows);
    }

    public Task<DataTable> GetDataTableAsync(
        string dataSourceName,
        IReadOnlyDictionary<string, object?> parameters,
        CancellationToken ct = default)
    {
        if (_tables.TryGetValue(dataSourceName, out var dt))
            return Task.FromResult(dt);
        return Task.FromResult(new DataTable(dataSourceName));
    }

    private static DataTable ToDataTable(string name, List<Dictionary<string, object?>> rows)
    {
        var dt = new DataTable(name);
        if (rows.Count == 0) return dt;
        foreach (var key in rows[0].Keys) dt.Columns.Add(key);
        foreach (var row in rows)
        {
            var dr = dt.NewRow();
            foreach (var (col, val) in row)
                if (dt.Columns.Contains(col))
                    dr[col] = val ?? DBNull.Value;
            dt.Rows.Add(dr);
        }
        return dt;
    }
}
