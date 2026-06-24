using ReportDesigner.Core.Rendering;
using ReportDesigner.Core.Model;

namespace ReportDesigner.Designer.Services;

/// <summary>
/// Holds a rendered ReportDocument and exposes it page-by-page for the
/// virtualised report viewer. Completely independent of the designer/preview flow.
/// </summary>
public sealed class ReportViewerService
{
    // ── State ─────────────────────────────────────────────────────────────────

    public ReportDocument?  Document   { get; private set; }
    public bool             HasReport  => Document is not null;
    public int              PageCount  => Document?.PageCount ?? 0;
    public int              CurrentPage { get; private set; } = 1;

    /// <summary>Fired when a new document is loaded or the page changes.</summary>
    public event Action?    OnChanged;

    // ── Group/bookmark index built at load time ───────────────────────────────

    /// <summary>
    /// List of bookmarks shown in the left panel.
    /// Each entry = (label, first page number).
    /// </summary>
    public List<ViewerBookmark> Bookmarks { get; } = [];

    // ── Load ──────────────────────────────────────────────────────────────────

    public void Load(ReportDocument doc)
    {
        Document    = doc;
        CurrentPage = 1;
        BuildBookmarks(doc);
        OnChanged?.Invoke();
    }

    public void Clear()
    {
        Document = null;
        Bookmarks.Clear();
        CurrentPage = 1;
        OnChanged?.Invoke();
    }

    // ── Navigation ────────────────────────────────────────────────────────────

    public void GoTo(int page)
    {
        if (Document is null) return;
        CurrentPage = Math.Clamp(page, 1, PageCount);
        OnChanged?.Invoke();
    }

    // ── Page retrieval ────────────────────────────────────────────────────────

    /// <summary>Returns the pages in the render window around <paramref name="center"/>.</summary>
    public IEnumerable<RenderedPage> GetWindow(int center, int radius = 5)
    {
        if (Document is null) yield break;
        int from = Math.Max(1,          center - radius);
        int to   = Math.Min(PageCount,  center + radius);
        for (int i = from; i <= to; i++)
            yield return Document.Pages[i - 1];
    }

    public RenderedPage? GetPage(int pageNumber)
    {
        if (Document is null || pageNumber < 1 || pageNumber > PageCount) return null;
        return Document.Pages[pageNumber - 1];
    }

    // ── Bookmark builder ──────────────────────────────────────────────────────

    private void BuildBookmarks(ReportDocument doc)
    {
        Bookmarks.Clear();

        // Group by 10s for large reports; for small ones list every page
        if (doc.PageCount <= 20)
        {
            for (int i = 1; i <= doc.PageCount; i++)
                Bookmarks.Add(new ViewerBookmark($"Page {i}", i, BookmarkKind.Page));
        }
        else
        {
            // Add decade markers
            for (int i = 1; i <= doc.PageCount; i += 10)
            {
                int to = Math.Min(i + 9, doc.PageCount);
                Bookmarks.Add(new ViewerBookmark(
                    i == to ? $"Page {i}" : $"Pages {i}–{to}",
                    i, BookmarkKind.Range));
            }
        }

        // Prepend render date bookmark
        Bookmarks.Insert(0, new ViewerBookmark(
            $"Rendered {DateTime.Now:yyyy-MM-dd HH:mm}",
            1, BookmarkKind.Header));
    }
}

public record ViewerBookmark(string Label, int PageNumber, BookmarkKind Kind);

public enum BookmarkKind { Header, Page, Range, Group }
