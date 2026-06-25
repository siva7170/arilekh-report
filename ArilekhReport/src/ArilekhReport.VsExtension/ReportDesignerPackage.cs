using ArilekhReport.VsExtension.Commands;
using ArilekhReport.VsExtension.Editor;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.OLE.Interop;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.Win32;
using System;
using System.ComponentModel.Design;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Task = System.Threading.Tasks.Task;

namespace ArilekhReport.VsExtension
{
    
    [PackageRegistration(UseManagedResourcesOnly = true, AllowsBackgroundLoading = true)]
    [Guid(ReportDesignerPackage.PackageGuidString)]
    [SuppressMessage("StyleCop.CSharp.DocumentationRules", "SA1650:ElementDocumentationMustBeSpelledCorrectly", Justification = "pkgdef, VS and vsixmanifest are valid VS terms")]

    [ProvideAutoLoad(VSConstants.UICONTEXT.SolutionExists_string,
                     PackageAutoLoadFlags.BackgroundLoad)]

    // Register the custom editor factory for .rdx files
    [ProvideEditorFactory(typeof(RdxEditorFactory),
                          110,                              // resource ID for display name
                          CommonPhysicalViewAttributes = (int)__VSPHYSICALVIEWATTRIBUTES.PVA_SupportsPreview,
                          TrustLevel = __VSEDITORTRUSTLEVEL.ETL_AlwaysTrusted)]
    [ProvideEditorLogicalView(typeof(RdxEditorFactory), VSConstants.LOGVIEWID.Designer_string)]
    [ProvideEditorExtension(typeof(RdxEditorFactory), ".rdx", 50,
                            ProjectGuid = "{8e6edf5e-75fe-42ae-b637-4e738158b08a}",
                            TemplateDir = @"Templates\ProjectItems",
                            NameResourceID = 110)]

    // Register the custom editor factory for .rds files
    [ProvideEditorFactory(typeof(RdsEditorFactory),
                          111,
                          TrustLevel = __VSEDITORTRUSTLEVEL.ETL_AlwaysTrusted)]
    [ProvideEditorLogicalView(typeof(RdsEditorFactory), VSConstants.LOGVIEWID.Designer_string)]
    [ProvideEditorExtension(typeof(RdsEditorFactory), ".rds", 50,
                            ProjectGuid = "{8e6edf5e-75fe-42ae-b637-4e738158b08a}",
                            NameResourceID = 111)]

    [ProvideMenuResource("Menus.ctmenu", 4)]
    public sealed class ReportDesignerPackage : AsyncPackage
    {
        public const string PackageGuidString = "c8aad8d9-cd73-4d6a-be4c-2540f4432041";

   
        public ReportDesignerPackage()
        {
        }

       
        protected override async Task InitializeAsync(CancellationToken cancellationToken, IProgress<ServiceProgressData> progress)
        {
           
            await this.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);

            RegisterEditorFactory(new RdxEditorFactory(this));
            RegisterEditorFactory(new RdsEditorFactory(this));

            // Register menu commands
            await OpenReportCommand.InitializeAsync(this);
            await NewReportCommand.InitializeAsync(this);
            await EditSchemaCommand.InitializeAsync(this);
        }
    }
}
