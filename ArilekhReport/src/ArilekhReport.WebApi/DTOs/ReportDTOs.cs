using ArilekhReport.Core.Model;
using ArilekhReport.Core.Rendering;

namespace ArilekhReport.WebApi.DTOs;

// ── Render request ─────────────────────────────────────────────────────────

/// <summary>
/// Posted to POST /api/reports/render to kick off report generation.
/// The caller supplies the .rdx XML and a data payload keyed by DataSource name.
/// </summary>
public sealed class RenderRequest
{
    /// <summary>Full .rdx XML string of the report definition.</summary>
    public string ReportXml { get; set; } = string.Empty;

    /// <summary>
    /// Data for each named data source.
    /// Key = DataSource name (matches DataSourceDefinition.Name in the rdx).
    /// Value = rows as array-of-objects; column names must match schema field names.
    /// </summary>
    public Dictionary<string, List<Dictionary<string, object?>>> Data { get; set; } = [];

    /// <summary>Optional named parameters passed to expressions.</summary>
    public Dictionary<string, object?> Parameters { get; set; } = [];

    /// <summary>TTL in minutes before the session is evicted. Default 30.</summary>
    public int TtlMinutes { get; set; } = 30;
}

// ── Render response ────────────────────────────────────────────────────────

/// <summary>Returned from POST /api/reports/render.</summary>
public sealed class RenderResponse
{
    public string SessionId    { get; set; } = string.Empty;
    public int    PageCount    { get; set; }
    public float  PageWidthPt  { get; set; }
    public float  PageHeightPt { get; set; }
    public string RenderedAt   { get; set; } = string.Empty;
    public long   RenderMs     { get; set; }
}

// ── Page response ──────────────────────────────────────────────────────────

/// <summary>Returned from GET /api/reports/{id}/pages/{n}.</summary>
public sealed class PageResponse
{
    public int             PageNumber { get; set; }
    public int             TotalPages { get; set; }
    public float           WidthPt    { get; set; }
    public float           HeightPt   { get; set; }
    public List<ElementDto> Elements  { get; set; } = [];
}

// ── Element DTOs (polymorphic via Type discriminator) ─────────────────────

public sealed class ElementDto
{
    public string Type        { get; set; } = string.Empty;  // text | rect | line | ellipse | image
    public float  X           { get; set; }
    public float  Y           { get; set; }
    public float  Width       { get; set; }
    public float  Height      { get; set; }
    public float  Rotation    { get; set; }

    // text
    public string? Text          { get; set; }
    public string? Alignment     { get; set; }   // left | center | right
    public string? VerticalAlign { get; set; }   // top | middle | bottom
    public string? HyperlinkUrl  { get; set; }

    // style (shared by text)
    public StyleDto? Style { get; set; }

    // rect
    public string? FillColor    { get; set; }
    public string? StrokeColor  { get; set; }
    public float   StrokeWidth  { get; set; }
    public float   BorderRadius { get; set; }

    // line
    public float X2 { get; set; }
    public float Y2 { get; set; }

    // image
    public string? Src     { get; set; }
    public string? Stretch { get; set; }
}

public sealed class StyleDto
{
    public string? FontFamily   { get; set; }
    public float?  FontSize     { get; set; }
    public bool    Bold         { get; set; }
    public bool    Italic       { get; set; }
    public bool    Underline    { get; set; }
    public string? ForeColor    { get; set; }
    public string? BackColor    { get; set; }
    public float   PaddingLeft  { get; set; }
    public float   PaddingRight { get; set; }
}

// ── Thumbnail response ─────────────────────────────────────────────────────

/// <summary>Returned from GET /api/reports/{id}/thumbnails.</summary>
public sealed class ThumbnailListResponse
{
    public string SessionId { get; set; } = string.Empty;
    public int    PageCount { get; set; }
    /// <summary>Text summary per page for thumbnail labels (first text element on page).</summary>
    public List<PageSummary> Pages { get; set; } = [];
}

public sealed class PageSummary
{
    public int    PageNumber   { get; set; }
    public string FirstText    { get; set; } = string.Empty;
    public string GroupLabel   { get; set; } = string.Empty;
}

// ── Server-side render request ─────────────────────────────────────────────

/// <summary>
/// Used by POST /api/reports/render-server.
/// The API resolves data server-side via a registered IServerDataProvider.
/// Angular only receives the session GUID — raw data never crosses the wire.
/// </summary>
public sealed class ServerRenderRequest
{
    public string  ReportXml       { get; set; } = string.Empty;
    /// <summary>Key identifying a registered IServerDataProvider (registered at startup).</summary>
    public string  DataProviderKey { get; set; } = string.Empty;
    /// <summary>Runtime parameters forwarded to the data provider and expressions.</summary>
    public Dictionary<string, object?> Parameters { get; set; } = [];
    public int     TtlMinutes      { get; set; } = 30;
}


public sealed class SearchResponse
{
    public string Query   { get; set; } = string.Empty;
    public int    Hits    { get; set; }
    public List<SearchHit> Results { get; set; } = [];
}

public sealed class SearchHit
{
    public int    PageNumber { get; set; }
    public string Snippet    { get; set; } = string.Empty;
    public float  X          { get; set; }
    public float  Y          { get; set; }
}
