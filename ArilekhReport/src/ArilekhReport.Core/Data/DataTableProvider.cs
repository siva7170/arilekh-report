using System;
using System.Collections.Generic;
using System.Text;
using System.Data;

namespace ArilekhReport.Core.Data;

/// <summary>
/// The simplest built-in <see cref="IDataSourceProvider"/>.
/// The caller registers one or more <see cref="DataTable"/> objects by name
/// before passing this provider to the engine.
///
/// <example>
/// <code>
/// var provider = new DataTableProvider();
/// provider.Register("Orders", myOrdersTable);
/// provider.Register("OrderLines", myLinesTable);
///
/// var engine = new ReportEngine();
/// var doc    = await engine.RenderAsync(reportDef, provider);
/// </code>
/// </example>
/// </summary>
public sealed class DataTableProvider : IDataSourceProvider
{
    private readonly Dictionary<string, DataTable> _tables =
        new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Registers a <see cref="DataTable"/> under the given logical name.</summary>
    public void Register(string name, DataTable table)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(table);
        _tables[name] = table;
    }

    /// <summary>Convenience overload: register using the table's own <c>TableName</c>.</summary>
    public void Register(DataTable table)
    {
        ArgumentNullException.ThrowIfNull(table);
        if (string.IsNullOrWhiteSpace(table.TableName))
            throw new ArgumentException("DataTable must have a non-empty TableName.", nameof(table));
        _tables[table.TableName] = table;
    }

    /// <inheritdoc />
    public Task<DataTable> GetDataTableAsync(
        string dataSourceName,
        IReadOnlyDictionary<string, object?> parameters,
        CancellationToken cancellationToken = default)
    {
        if (!_tables.TryGetValue(dataSourceName, out var table))
            throw new KeyNotFoundException(
                $"DataTableProvider: no DataTable registered for data source '{dataSourceName}'. " +
                $"Available: {string.Join(", ", _tables.Keys)}");

        return Task.FromResult(table);
    }
}