using System;
using System.Collections.Generic;
using System.Text;
using System.Data;
using ArilekhReport.Core.Model;

namespace ArilekhReport.Core.Expressions;

/// <summary>
/// Mutable bag of state passed through the entire render pipeline.
/// The expression evaluator reads from this context to resolve
/// Fields.*, Parameters.*, aggregates, and page variables.
/// </summary>
public sealed class RenderContext
{
    // ── Report-level ──────────────────────────────────────────────────────────

    public ReportDefinition Report { get; }
    public IReadOnlyDictionary<string, object?> Parameters { get; }

    // ── Current data state ────────────────────────────────────────────────────

    /// <summary>The DataTable currently being iterated (Detail / Group sections).</summary>
    public DataTable? CurrentTable { get; set; }

    /// <summary>The DataRow currently being rendered. Null outside a Detail band.</summary>
    public DataRow? CurrentRow { get; set; }

    /// <summary>Zero-based row index within <see cref="CurrentTable"/>.</summary>
    public int RowIndex { get; set; } = -1;

    // ── Group tracking ────────────────────────────────────────────────────────

    /// <summary>
    /// Tracks the current group key value for each group field name.
    /// Updated whenever a new GroupHeader is entered.
    /// </summary>
    public Dictionary<string, object?> CurrentGroupValues { get; } = new();

    // ── Aggregate accumulators  (keyed by field name) ─────────────────────────

    /// <summary>Running sum per field name within the current group / report.</summary>
    public Dictionary<string, decimal> RunningSum { get; } = new();

    /// <summary>Running count per data-source name.</summary>
    public Dictionary<string, int> RunningCount { get; } = new();

    /// <summary>Running min per field name.</summary>
    public Dictionary<string, object?> RunningMin { get; } = new();

    /// <summary>Running max per field name.</summary>
    public Dictionary<string, object?> RunningMax { get; } = new();

    // ── Page tracking ─────────────────────────────────────────────────────────

    public int PageNumber { get; set; } = 1;

    /// <summary>
    /// Total page count.  Only available after a two-pass render.
    /// Set to -1 (unknown) during the first pass.
    /// </summary>
    public int TotalPages { get; set; } = -1;

    // ── Document geometry ─────────────────────────────────────────────────────

    /// <summary>Y-cursor position (points) within the current page's printable area.</summary>
    public float CurrentY { get; set; } = 0f;

    // ── Constructor ───────────────────────────────────────────────────────────

    public RenderContext(
        ReportDefinition report,
        IReadOnlyDictionary<string, object?> parameters)
    {
        Report = report;
        Parameters = parameters;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Returns the value of the named field from the current row.
    /// Returns <see langword="null"/> if the column does not exist or the value is DBNull.
    /// </summary>
    public object? GetFieldValue(string fieldName)
    {
        if (CurrentRow is null) return null;
        if (!CurrentRow.Table.Columns.Contains(fieldName)) return null;
        var v = CurrentRow[fieldName];
        return v == DBNull.Value ? null : v;
    }

    /// <summary>Resets all running aggregates for a new group.</summary>
    public void ResetGroupAggregates()
    {
        RunningSum.Clear();
        RunningCount.Clear();
        RunningMin.Clear();
        RunningMax.Clear();
    }

    /// <summary>Accumulates aggregate values for the current row.</summary>
    public void AccumulateRow(DataRow row)
    {
        foreach (DataColumn col in row.Table.Columns)
        {
            var val = row[col];
            if (val == DBNull.Value) continue;

            var key = col.ColumnName;

            // Sum / Count
            if (val is IConvertible cv)
            {
                try
                {
                    var d = Convert.ToDecimal(cv);
                    RunningSum[key] = RunningSum.GetValueOrDefault(key, 0m) + d;
                }
                catch { /* non-numeric column */ }
            }

            RunningCount[key] = RunningCount.GetValueOrDefault(key, 0) + 1;

            // Min
            if (!RunningMin.TryGetValue(key, out var curMin) || curMin is null)
                RunningMin[key] = val;
            else if (val is IComparable cmpVal && cmpVal.CompareTo(curMin) < 0)
                RunningMin[key] = val;

            // Max
            if (!RunningMax.TryGetValue(key, out var curMax) || curMax is null)
                RunningMax[key] = val;
            else if (val is IComparable cmpMax && cmpMax.CompareTo(curMax) > 0)
                RunningMax[key] = val;
        }
    }
}