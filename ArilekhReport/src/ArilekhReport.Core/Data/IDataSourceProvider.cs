using System;
using System.Collections.Generic;
using System.Text;
using System.Data;
using ArilekhReport.Core.Model;

namespace ArilekhReport.Core.Data;

/// <summary>
/// Abstraction that the runtime engine uses to obtain data for each named data source.
/// Implement this interface to connect any data backend (EF Core, ADO.NET, REST, JSON, …).
/// </summary>
public interface IDataSourceProvider
{
    /// <summary>
    /// Returns a populated <see cref="DataTable"/> for the named data source.
    /// </summary>
    /// <param name="dataSourceName">
    ///   The <see cref="DataSourceDefinition.Name"/> value
    ///   declared in the report definition.
    /// </param>
    /// <param name="parameters">
    ///   Runtime parameter values passed to the report.
    ///   The key is the parameter name; the value is the typed object.
    /// </param>
    /// <param name="cancellationToken">Cancellation support for async I/O.</param>
    Task<DataTable> GetDataTableAsync(
        string dataSourceName,
        IReadOnlyDictionary<string, object?> parameters,
        CancellationToken cancellationToken = default);
}