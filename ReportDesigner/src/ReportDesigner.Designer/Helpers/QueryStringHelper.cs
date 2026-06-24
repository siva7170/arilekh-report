namespace ReportDesigner.Designer.Helpers;

/// <summary>
/// Lightweight query string parser for Blazor WASM
/// (System.Web.HttpUtility is not available in net10.0 WASM).
/// </summary>
public static class QueryStringHelper
{
    /// <summary>
    /// Parses a query string like "?file=foo&amp;type=rdx" into a dictionary.
    /// </summary>
    public static Dictionary<string, string> Parse(string queryString)
    {
        var result = new Dictionary<string, string>(
            StringComparer.OrdinalIgnoreCase);

        if (string.IsNullOrWhiteSpace(queryString))
            return result;

        var query = queryString.TrimStart('?');
        foreach (var pair in query.Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var idx = pair.IndexOf('=');
            if (idx <= 0) continue;

            var key = Uri.UnescapeDataString(pair[..idx].Replace('+', ' '));
            var val = Uri.UnescapeDataString(pair[(idx + 1)..].Replace('+', ' '));
            result[key] = val;
        }

        return result;
    }
}
