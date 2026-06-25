using System;
using System.ComponentModel.Design;
using System.IO;
using ArilekhReport.VsExtension;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Task = System.Threading.Tasks.Task;

namespace ArilekhReport.VsExtension.Commands
{
    /// <summary>
    /// Command: Report Designer → Open Report…
    /// Appears in the Tools menu and in the solution explorer context menu for .rdx files.
    /// </summary>
    internal sealed class OpenReportCommand
    {
        private readonly AsyncPackage _package;

        private OpenReportCommand(AsyncPackage package, OleMenuCommandService commandService)
        {
            _package = package;

            var cmdId = new CommandID(
                ArilekhReport.VsExtension.PackageGuids.guidReportDesignerPackageCmdSet,
                PackageCommandIds.OpenReport);

            var cmd = new OleMenuCommand(Execute, cmdId);
            cmd.BeforeQueryStatus += OnBeforeQueryStatus;
            commandService.AddCommand(cmd);
        }

        public static async Task InitializeAsync(AsyncPackage package)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(
                package.DisposalToken);

            var commandService = await package.GetServiceAsync(
                typeof(IMenuCommandService)) as OleMenuCommandService;

            if (commandService is not null)
                new OpenReportCommand(package, commandService);
        }

        private void OnBeforeQueryStatus(object sender, EventArgs e)
        {
            // Always visible
            if (sender is OleMenuCommand cmd)
                cmd.Visible = cmd.Enabled = true;
        }

        private void Execute(object sender, EventArgs e)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Title  = "Open Report Definition",
                Filter = "Report Definition (*.rdx)|*.rdx|All Files (*.*)|*.*",
            };

            if (dialog.ShowDialog() != true) return;

            OpenRdxFile(dialog.FileName);
        }

        internal static void OpenRdxFile(string filePath)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            // Ask VS to open the file – our registered editor factory will handle it
            VsShellUtilities.OpenDocument(
                ServiceProvider.GlobalProvider,
                filePath,
                PkgGUID.RdxEditorFactory,
                out _,
                out _,
                out _);
        }
    }
}
