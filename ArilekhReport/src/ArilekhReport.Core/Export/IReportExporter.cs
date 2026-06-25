using ArilekhReport.Core.Rendering;
using System;
using System.Collections.Generic;
using System.Text;

namespace ArilekhReport.Core.Export
{
    public interface IReportExporter
    {
        /// <summary>Export to a byte array (suitable for HTTP responses or file writes).</summary>
        byte[] Export(ReportDocument document);

        /// <summary>Export and write directly to a file.</summary>
        void ExportToFile(ReportDocument document, string filePath);

        /// <summary>Suggested file extension including the dot, e.g. ".pdf".</summary>
        string FileExtension { get; }

        /// <summary>MIME type for HTTP Content-Type header.</summary>
        string MimeType { get; }
    }

}
