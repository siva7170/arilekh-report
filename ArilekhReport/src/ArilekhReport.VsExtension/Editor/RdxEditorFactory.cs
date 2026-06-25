using System;
using System.Runtime.InteropServices;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.OLE.Interop;
using ArilekhReport.VsExtension;

namespace ArilekhReport.VsExtension.Editor
{
    /// <summary>
    /// Editor factory for <c>.rdx</c> report definition files.
    /// When VS opens a .rdx file this factory creates a <see cref="ReportDesignerPane"/>
    /// hosting the Blazor WASM designer in a WebView2 control.
    /// </summary>
    [Guid(PkgGUID.RdxEditorFactoryString)]
    public sealed class RdxEditorFactory : IVsEditorFactory
    {
        private readonly AsyncPackage _package;
        private Microsoft.VisualStudio.OLE.Interop.IServiceProvider _oleServiceProvider;

        public RdxEditorFactory(AsyncPackage package) => _package = package;

        public int SetSite(Microsoft.VisualStudio.OLE.Interop.IServiceProvider psp)
        {
            _oleServiceProvider = psp;
            return VSConstants.S_OK;
        }

        public int MapLogicalView(ref Guid rguidLogicalView, out string pbstrPhysicalView)
        {
            pbstrPhysicalView = null;

            if (rguidLogicalView == VSConstants.LOGVIEWID.Designer_guid ||
                rguidLogicalView == VSConstants.LOGVIEWID.Primary_guid)
                return VSConstants.S_OK;

            return VSConstants.E_NOTIMPL;
        }

        public int CreateEditorInstance(
            uint           grfCreateDoc,
            string         pszMkDocument,
            string         pszPhysicalView,
            IVsHierarchy   pvHier,
            uint           itemid,
            IntPtr         punkDocDataExisting,
            out IntPtr     ppunkDocView,
            out IntPtr     ppunkDocData,
            out string     pbstrEditorCaption,
            out Guid       pguidCmdUI,
            out int        pgrfCDW)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            ppunkDocView       = IntPtr.Zero;
            ppunkDocData       = IntPtr.Zero;
            pbstrEditorCaption = " [Report Designer]";
            pguidCmdUI         = Guid.Empty;
            pgrfCDW            = 0;

            // Reuse existing doc data if already open
            if (punkDocDataExisting != IntPtr.Zero)
            {
                ppunkDocData = punkDocDataExisting;
                Marshal.AddRef(ppunkDocData);
            }
            else
            {
                var docData = new ReportDocumentData(pszMkDocument, FileType.Rdx);
                ppunkDocData = Marshal.GetIUnknownForObject(docData);
            }

            // Create the designer pane (WebView2 host)
            var pane = new ReportDesignerPane(_package, pszMkDocument, FileType.Rdx);
            ppunkDocView = Marshal.GetIUnknownForObject(pane);

            return VSConstants.S_OK;
        }

        public int Close() => VSConstants.S_OK;
    }

    /// <summary>
    /// Editor factory for <c>.rds</c> dataset schema files.
    /// Opens the schema editor tab inside the same designer pane.
    /// </summary>
    [Guid(PkgGUID.RdsEditorFactoryString)]
    public sealed class RdsEditorFactory : IVsEditorFactory
    {
        private readonly AsyncPackage _package;

        public RdsEditorFactory(AsyncPackage package) => _package = package;

        public int SetSite(Microsoft.VisualStudio.OLE.Interop.IServiceProvider psp)
            => VSConstants.S_OK;

        public int MapLogicalView(ref Guid rguidLogicalView, out string pbstrPhysicalView)
        {
            pbstrPhysicalView = null;
            return (rguidLogicalView == VSConstants.LOGVIEWID.Designer_guid ||
                    rguidLogicalView == VSConstants.LOGVIEWID.Primary_guid)
                ? VSConstants.S_OK
                : VSConstants.E_NOTIMPL;
        }

        public int CreateEditorInstance(
            uint grfCreateDoc, string pszMkDocument, string pszPhysicalView,
            IVsHierarchy pvHier, uint itemid, IntPtr punkDocDataExisting,
            out IntPtr ppunkDocView, out IntPtr ppunkDocData,
            out string pbstrEditorCaption, out Guid pguidCmdUI, out int pgrfCDW)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            ppunkDocView       = IntPtr.Zero;
            ppunkDocData       = IntPtr.Zero;
            pbstrEditorCaption = " [Schema Editor]";
            pguidCmdUI         = Guid.Empty;
            pgrfCDW            = 0;

            if (punkDocDataExisting != IntPtr.Zero)
            {
                ppunkDocData = punkDocDataExisting;
                Marshal.AddRef(ppunkDocData);
            }
            else
            {
                var docData = new ReportDocumentData(pszMkDocument, FileType.Rds);
                ppunkDocData = Marshal.GetIUnknownForObject(docData);
            }

            var pane = new ReportDesignerPane(_package, pszMkDocument, FileType.Rds);
            ppunkDocView = Marshal.GetIUnknownForObject(pane);

            return VSConstants.S_OK;
        }

        public int Close() => VSConstants.S_OK;
    }

    /// <summary>Identifies which file type the pane is editing.</summary>
    public enum FileType { Rdx, Rds }
}
