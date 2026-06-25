using System;
using System.Collections.Generic;
using System.Text;
using System.Data;
using ArilekhReport.Core.Model;

namespace ArilekhReport.Core.Data;

/// <summary>
/// <see cref="IDataSourceProvider"/> that serves the sample data rows embedded inside
/// <see cref="DataSetSchema"/> (.rds) files.  Used by the designer for live preview
/// without a real database connection.
/// </summary>
public sealed class SampleDataProvider : IDataSourceProvider
{
    private readonly Dictionary<string, DataSetSchema> _schemas =
        new(StringComparer.OrdinalIgnoreCase);

    // ── Registration ──────────────────────────────────────────────────────────

    /// <summary>Registers a schema (and its embedded sample rows) under the given name.</summary>
    public void Register(DataSetSchema schema)
    {
        ArgumentNullException.ThrowIfNull(schema);
        _schemas[schema.Name] = schema;
    }

    /// <summary>Loads all .rds files in the given directory and registers them.</summary>
    public void RegisterFromDirectory(string directory)
    {
        foreach (var file in Directory.EnumerateFiles(directory, "*.rds"))
        {
            var schema = XmlReportSerializer.LoadSchema(file);
            Register(schema);
        }
    }

    // ── IDataSourceProvider ───────────────────────────────────────────────────

    /// <inheritdoc />
    public Task<DataTable> GetDataTableAsync(
        string dataSourceName,
        IReadOnlyDictionary<string, object?> parameters,
        CancellationToken cancellationToken = default)
    {
        if (!_schemas.TryGetValue(dataSourceName, out var schema))
            throw new KeyNotFoundException(
                $"SampleDataProvider: no schema registered for '{dataSourceName}'.");

        return Task.FromResult(schema.ToSampleDataTable());
    }
}