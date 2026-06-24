using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ReportDesigner.VsExtension
{
    internal static class PkgGUID
    {
        // Package
        public const string PackageGuidString = ReportDesigner.VsExtension.PackageGuids.guidReportDesignerPackageString;
        public static readonly Guid PackageGuid = new Guid(PackageGuidString);

        // Editor factories
        public const string RdxEditorFactoryString = "A1B2C3D4-1002-0000-0000-000000000001";
        public static readonly Guid RdxEditorFactory = new Guid(RdxEditorFactoryString);

        public const string RdsEditorFactoryString = "A1B2C3D4-1003-0000-0000-000000000001";
        public static readonly Guid RdsEditorFactory = new Guid(RdsEditorFactoryString);
    }

    /// <summary>Command IDs – must match Menus.vsct.</summary>
    internal static class PackageCommandIds
    {
        public const int OpenReport = 0x0102;
        public const int NewReport = 0x0100;
        public const int EditSchema = 0x0103;
    }
}
