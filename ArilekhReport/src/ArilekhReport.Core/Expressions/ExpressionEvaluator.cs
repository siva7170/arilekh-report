using NCalc;
using NCalc.Extensions;
using ArilekhReport.Core.Model;

namespace ArilekhReport.Core.Expressions;

/// <summary>
/// Evaluates field expressions against a <see cref="RenderContext"/>.
///
/// Supported expression syntax:
/// <list type="bullet">
///   <item><c>Fields.FieldName</c>            – value from the current DataRow</item>
///   <item><c>Parameters.ParamName</c>         – report parameter value</item>
///   <item><c>Sum("FieldName")</c>             – running sum for the current group</item>
///   <item><c>Count()</c>                      – running row count</item>
///   <item><c>Avg("FieldName")</c>             – running average</item>
///   <item><c>Min("FieldName")</c>             – running minimum</item>
///   <item><c>Max("FieldName")</c>             – running maximum</item>
///   <item><c>PageNumber()</c>                 – current page number</item>
///   <item><c>TotalPages()</c>                 – total page count (two-pass)</item>
///   <item><c>Today()</c>                      – today's date</item>
///   <item><c>Now()</c>                        – current date+time</item>
///   <item><c>Format(value, "fmt")</c>         – .NET format string</item>
///   <item><c>Iif(condition, trueVal, falseVal)</c> – inline conditional</item>
///   <item><c>IsNull(value, fallback)</c>      – null-coalescing</item>
///   <item><c>Len("text")</c>                  – string length</item>
///   <item><c>Upper("text")</c> / <c>Lower("text")</c></item>
///   <item><c>Trim("text")</c></item>
///   <item><c>Substring("text", start, len)</c></item>
///   <item><c>RowNumber()</c>                  – 1-based row index within the current Detail section</item>
/// </list>
/// </summary>
public sealed class ExpressionEvaluator
{
    // ── Public entry point ────────────────────────────────────────────────────

    /// <summary>
    /// Evaluates <paramref name="expression"/> and returns the raw value.
    /// Returns <see langword="null"/> on evaluation errors (logs to debug output).
    /// </summary>
    public object? Evaluate(string expression, RenderContext ctx)
    {
        if (string.IsNullOrWhiteSpace(expression))
            return null;

        // Pre-process: replace Fields.Xxx → [Fields.Xxx] tokens that NCalc can handle
        var preprocessed = PreprocessExpression(expression, ctx);

        // If after preprocessing the expression is a literal string constant, return it
        if (preprocessed.StartsWith("'") && preprocessed.EndsWith("'") && preprocessed.Length >= 2)
            return preprocessed[1..^1];

        try
        {
            var e = new Expression(preprocessed);

            // Register parameters from the context
            RegisterParameters(e, ctx);

            // Register custom functions
            //e.EvaluateFunction += (name, args) => EvaluateFunction(name, args, ctx);


            RegisterFunctions(e, ctx);

            return e.Evaluate();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(
                $"[ExpressionEvaluator] Error evaluating '{expression}': {ex.Message}");
            return null;
        }
    }

    private static void RegisterFunctions(Expression e, RenderContext ctx)
    {
        // Aggregates

        e.Functions["SUM"] = args =>
        {
            var field = args.Evaluate(0)?.ToString() ?? "";
            return ctx.RunningSum.GetValueOrDefault(field, 0m);
        };

        e.Functions["COUNT"] = args =>
        {
            string? field = args.Count > 0
                ? args.Evaluate(0)?.ToString()
                : ctx.CurrentTable?.TableName;

            return field is null
                ? 0
                : ctx.RunningCount.GetValueOrDefault(field, 0);
        };

        e.Functions["AVG"] = args =>
        {
            var field = args.Evaluate(0)?.ToString() ?? "";

            var sum = ctx.RunningSum.GetValueOrDefault(field, 0m);
            var count = ctx.RunningCount.GetValueOrDefault(field, 0);

            return count == 0 ? 0m : sum / count;
        };

        e.Functions["MIN"] = args =>
        {
            var field = args.Evaluate(0)?.ToString() ?? "";
            return ctx.RunningMin.GetValueOrDefault(field);
        };

        e.Functions["MAX"] = args =>
        {
            var field = args.Evaluate(0)?.ToString() ?? "";
            return ctx.RunningMax.GetValueOrDefault(field);
        };

        // Page Functions

        e.Functions["PAGENUMBER"] = _ => ctx.PageNumber;

        e.Functions["TOTALPAGES"] = _ =>
            ctx.TotalPages == -1 ? "?" : ctx.TotalPages;

        e.Functions["ROWNUMBER"] = _ =>
            ctx.RowIndex + 1;

        // Date / Time

        e.Functions["TODAY"] = _ =>
            DateTime.Today;

        e.Functions["NOW"] = _ =>
            DateTime.Now;

        // Formatting

        e.Functions["FORMAT"] = args =>
        {
            var value = args.Evaluate(0);
            var format = args.Evaluate(1)?.ToString() ?? "";

            return value is IFormattable formattable
                ? formattable.ToString(format, null)
                : value?.ToString() ?? "";
        };

        // Conditional

        e.Functions["IIF"] = args =>
        {
            var condition = args.Evaluate(0);

            bool isTrue = condition switch
            {
                bool b => b,
                string s => s.Equals("TRUE", StringComparison.OrdinalIgnoreCase),
                _ => false
            };

            return isTrue
                ? args.Evaluate(1)
                : args.Evaluate(2);
        };

        // Null Handling

        e.Functions["ISNULL"] = args =>
        {
            var value = args.Evaluate(0);
            return value ?? args.Evaluate(1);
        };

        // String Functions

        e.Functions["LEN"] = args =>
        {
            return args.Evaluate(0)?.ToString()?.Length ?? 0;
        };

        e.Functions["UPPER"] = args =>
        {
            return args.Evaluate(0)?.ToString()?.ToUpper() ?? "";
        };

        e.Functions["LOWER"] = args =>
        {
            return args.Evaluate(0)?.ToString()?.ToLower() ?? "";
        };

        e.Functions["TRIM"] = args =>
        {
            return args.Evaluate(0)?.ToString()?.Trim() ?? "";
        };

        e.Functions["SUBSTRING"] = args =>
        {
            var str = args.Evaluate(0)?.ToString() ?? "";

            int start = Convert.ToInt32(args.Evaluate(1));

            int len = args.Count > 2
                ? Convert.ToInt32(args.Evaluate(2))
                : str.Length - start;

            start = Math.Max(0, start);

            if (start >= str.Length)
                return "";

            len = Math.Min(len, str.Length - start);

            return str.Substring(start, len);
        };

        e.Functions["CONCAT"] = args =>
        {
            return string.Concat(
                Enumerable.Range(0, args.Count)
                          .Select(i => args.Evaluate(i)?.ToString() ?? ""));
        };

        // Numeric

        e.Functions["ROUND"] = args =>
        {
            decimal value = Convert.ToDecimal(args.Evaluate(0));

            int decimals = args.Count > 1
                ? Convert.ToInt32(args.Evaluate(1))
                : 2;

            return Math.Round(value, decimals);
        };
    }

    /// <summary>
    /// Evaluates an expression expected to return a boolean.
    /// Returns <see langword="false"/> on error or null.
    /// </summary>
    public bool EvaluateBool(string expression, RenderContext ctx)
    {
        var result = Evaluate(expression, ctx);
        if (result is bool b) return b;
        if (result is string s) return bool.TryParse(s, out var p) && p;
        return false;
    }

    // ── Pre-processing ────────────────────────────────────────────────────────

    private static string PreprocessExpression(string expr, RenderContext ctx)
    {
        // Replace Fields.XxxYyy with [Fields.XxxYyy]  (NCalc uses [...] for identifiers with dots)
        // Also replace Parameters.XxxYyy similarly
        var sb = new System.Text.StringBuilder();
        int i = 0;

        while (i < expr.Length)
        {
            // Check for Fields. or Parameters. prefix
            if (TryMatchPrefix(expr, i, "Fields.", out var suffix) ||
                TryMatchPrefix(expr, i, "Parameters.", out suffix))
            {
                int prefixLen = expr.IndexOf('.', i) + 1;
                int nameStart = i;
                int nameEnd = i + prefixLen + suffix!.Length;
                sb.Append('[');
                sb.Append(expr, nameStart, nameEnd - nameStart);
                sb.Append(']');
                i = nameEnd;
                continue;
            }

            sb.Append(expr[i]);
            i++;
        }

        return sb.ToString();
    }

    private static bool TryMatchPrefix(string expr, int pos, string prefix, out string? rest)
    {
        rest = null;
        if (pos + prefix.Length > expr.Length) return false;
        if (!expr.AsSpan(pos).StartsWith(prefix.AsSpan(), StringComparison.OrdinalIgnoreCase))
            return false;

        // Capture the identifier after the prefix (letters, digits, underscore)
        int nameStart = pos + prefix.Length;
        int j = nameStart;
        while (j < expr.Length && (char.IsLetterOrDigit(expr[j]) || expr[j] == '_'))
            j++;

        if (j == nameStart) return false; // no name after prefix
        rest = expr[nameStart..j];
        return true;
    }

    // ── NCalc parameter injection ─────────────────────────────────────────────

    private static void RegisterParameters(NCalc.Expression e, RenderContext ctx)
    {
        // Fields.* from current row
        if (ctx.CurrentRow is not null)
        {
            foreach (System.Data.DataColumn col in ctx.CurrentRow.Table.Columns)
            {
                var val = ctx.CurrentRow[col];
                e.Parameters[$"Fields.{col.ColumnName}"] =
                    val == System.DBNull.Value ? null : val;
            }
        }

        // Parameters.*
        foreach (var (k, v) in ctx.Parameters)
            e.Parameters[$"Parameters.{k}"] = v;
    }

    // ── Custom function handler ───────────────────────────────────────────────

    //private static void EvaluateFunction(
    //    string name,
    //    FunctionArgs args,
    //    RenderContext ctx)
    //{
    //    switch (name.ToUpperInvariant())
    //    {
    //        // ── Aggregates ─────────────────────────────────────────────────────

    //        case "SUM":
    //            {
    //                var field = args.Parameters[0].Evaluate()?.ToString() ?? "";
    //                args.Result = ctx.RunningSum.GetValueOrDefault(field, 0m);
    //                break;
    //            }
    //        case "COUNT":
    //            {
    //                var field = args.Parameters.Length > 0
    //                    ? args.Parameters[0].Evaluate()?.ToString()
    //                    : ctx.CurrentTable?.TableName;
    //                args.Result = field is null ? 0 : ctx.RunningCount.GetValueOrDefault(field, 0);
    //                break;
    //            }
    //        case "AVG":
    //            {
    //                var field = args.Parameters[0].Evaluate()?.ToString() ?? "";
    //                var sum = ctx.RunningSum.GetValueOrDefault(field, 0m);
    //                var cnt = ctx.RunningCount.GetValueOrDefault(field, 0);
    //                args.Result = cnt == 0 ? 0m : sum / cnt;
    //                break;
    //            }
    //        case "MIN":
    //            {
    //                var field = args.Parameters[0].Evaluate()?.ToString() ?? "";
    //                args.Result = ctx.RunningMin.GetValueOrDefault(field);
    //                break;
    //            }
    //        case "MAX":
    //            {
    //                var field = args.Parameters[0].Evaluate()?.ToString() ?? "";
    //                args.Result = ctx.RunningMax.GetValueOrDefault(field);
    //                break;
    //            }

    //        // ── Page functions ─────────────────────────────────────────────────

    //        case "PAGENUMBER":
    //            args.Result = ctx.PageNumber;
    //            break;

    //        case "TOTALPAGES":
    //            args.Result = ctx.TotalPages == -1 ? "?" : ctx.TotalPages;
    //            break;

    //        case "ROWNUMBER":
    //            args.Result = ctx.RowIndex + 1;
    //            break;

    //        // ── Date / time ───────────────────────────────────────────────────

    //        case "TODAY":
    //            args.Result = DateTime.Today;
    //            break;

    //        case "NOW":
    //            args.Result = DateTime.Now;
    //            break;

    //        // ── String functions ──────────────────────────────────────────────

    //        case "FORMAT":
    //            {
    //                var val = args.Parameters[0].Evaluate();
    //                var fmt = args.Parameters[1].Evaluate()?.ToString() ?? "";
    //                args.Result = val is IFormattable f ? f.ToString(fmt, null) : val?.ToString() ?? "";
    //                break;
    //            }
    //        case "IIF":
    //            {
    //                var cond = args.Parameters[0].Evaluate();
    //                var isTrue = cond is bool b ? b : (cond?.ToString()?.ToUpperInvariant() == "TRUE");
    //                args.Result = isTrue
    //                    ? args.Parameters[1].Evaluate()
    //                    : args.Parameters[2].Evaluate();
    //                break;
    //            }
    //        case "ISNULL":
    //            {
    //                var val = args.Parameters[0].Evaluate();
    //                args.Result = val ?? args.Parameters[1].Evaluate();
    //                break;
    //            }
    //        case "LEN":
    //            args.Result = args.Parameters[0].Evaluate()?.ToString()?.Length ?? 0;
    //            break;

    //        case "UPPER":
    //            args.Result = args.Parameters[0].Evaluate()?.ToString()?.ToUpper() ?? "";
    //            break;

    //        case "LOWER":
    //            args.Result = args.Parameters[0].Evaluate()?.ToString()?.ToLower() ?? "";
    //            break;

    //        case "TRIM":
    //            args.Result = args.Parameters[0].Evaluate()?.ToString()?.Trim() ?? "";
    //            break;

    //        case "SUBSTRING":
    //            {
    //                var str = args.Parameters[0].Evaluate()?.ToString() ?? "";
    //                var start = Convert.ToInt32(args.Parameters[1].Evaluate());
    //                var len = args.Parameters.Length > 2
    //                    ? Convert.ToInt32(args.Parameters[2].Evaluate())
    //                    : str.Length - start;
    //                args.Result = str.Substring(Math.Max(0, start), Math.Min(len, str.Length - start));
    //                break;
    //            }

    //        case "CONCAT":
    //            {
    //                var parts = args.Parameters.Select(p => p.Evaluate()?.ToString() ?? "");
    //                args.Result = string.Concat(parts);
    //                break;
    //            }

    //        case "ROUND":
    //            {
    //                var val = Convert.ToDecimal(args.Parameters[0].Evaluate());
    //                var decimals = args.Parameters.Length > 1
    //                    ? Convert.ToInt32(args.Parameters[1].Evaluate()) : 2;
    //                args.Result = Math.Round(val, decimals);
    //                break;
    //            }
    //    }
    //}
}