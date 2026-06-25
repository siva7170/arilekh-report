using System;
using System.ComponentModel.Design;
using System.IO;
using ArilekhReport.VsExtension;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using ReportDesigner.VsExtension.Wizard;
using Task = System.Threading.Tasks.Task;

namespace ArilekhReport.VsExtension.Commands
{
    /// <summary>
    /// Command: right-click a .rds file → "Edit Dataset Schema…"
    /// Opens the <see cref="DatasetSchemaWizard"/> WPF dialog.
    /// </summary>
    internal sealed class EditSchemaCommand
    {
        private readonly AsyncPackage _package;

        private EditSchemaCommand(AsyncPackage package, OleMenuCommandService commandService)
        {
            _package = package;

            var cmdId = new CommandID(
                ArilekhReport.VsExtension.PackageGuids.guidReportDesignerPackageCmdSet,
                PackageCommandIds.EditSchema);

            var cmd = new OleMenuCommand(Execute, cmdId);
            cmd.BeforeQueryStatus += OnBeforeQueryStatus;
            commandService.AddCommand(cmd);
        }

        public static async Task InitializeAsync(AsyncPackage package)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(
                package.DisposalToken);

            var svc = await package.GetServiceAsync(typeof(IMenuCommandService))
                      as OleMenuCommandService;
            if (svc is not null)
                new EditSchemaCommand(package, svc);
        }

        private void OnBeforeQueryStatus(object sender, EventArgs e)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            if (sender is not OleMenuCommand cmd) return;

            // Only show when a .rds file is selected in Solution Explorer
            var selectedFile = GetSelectedFilePath();
            cmd.Visible = cmd.Enabled =
                !string.IsNullOrEmpty(selectedFile) &&
                selectedFile.EndsWith(".rds", StringComparison.OrdinalIgnoreCase);
        }

        private void Execute(object sender, EventArgs e)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            var filePath = GetSelectedFilePath();
            if (string.IsNullOrEmpty(filePath)) return;

            // Load existing schema XML (or start fresh)
            string existingXml = File.Exists(filePath)
                ? File.ReadAllText(filePath, System.Text.Encoding.UTF8)
                : string.Empty;

            // Open dataset schema wizard
            var wizard = new DatasetSchemaWizard(filePath, existingXml);
            if (wizard.ShowDialog() == true && wizard.ResultXml is not null)
            {
                File.WriteAllText(filePath, wizard.ResultXml, System.Text.Encoding.UTF8);

                VsShellUtilities.ShowMessageBox(
                    _package,
                    $"Schema saved to {Path.GetFileName(filePath)}",
                    "Report Designer",
                    OLEMSGICON.OLEMSGICON_INFO,
                    OLEMSGBUTTON.OLEMSGBUTTON_OK,
                    OLEMSGDEFBUTTON.OLEMSGDEFBUTTON_FIRST);
            }
        }

        private static string GetSelectedFilePath()
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            try
            {
                var dte = ServiceProvider.GlobalProvider
                    .GetService(typeof(EnvDTE.DTE)) as EnvDTE.DTE;

                if (dte?.SelectedItems?.Count > 0)
                {
                    var item = dte.SelectedItems.Item(1);
                    return item?.ProjectItem?.FileNames[1] ?? string.Empty;
                }
            }
            catch { }

            return string.Empty;
        }
    }
}
