using System;
using System.ComponentModel.Design;
using System.IO;
using Microsoft.VisualStudio.Shell;
using Task = System.Threading.Tasks.Task;

namespace ReportDesigner.VsExtension.Commands
{
    /// <summary>
    /// Command: Report Designer → New Report…
    /// Creates a blank .rdx file in the selected project folder and opens the designer.
    /// </summary>
    internal sealed class NewReportCommand
    {
        private readonly AsyncPackage _package;

        private static readonly string _blankRdx = @"<?xml version=""1.0"" encoding=""utf-8""?>
<Report xmlns=""urn:reportdesigner"" Name=""NewReport"" Version=""1.0"">
  <PageSetup Size=""A4"" Orientation=""Portrait""
             MarginTop=""36"" MarginBottom=""36""
             MarginLeft=""36"" MarginRight=""36"" />
  <DefaultStyle FontFamily=""Arial"" FontSize=""9"" ForeColor=""#000000"" />
  <DataSources />
  <Parameters />
  <Sections>
    <Section Name=""PageHeader1"" Type=""PageHeader"" Height=""30"">
      <Fields>
        <Field Name=""Title"" Text=""Report Title""
               X=""0"" Y=""6"" Width=""200"" Height=""16"">
          <Style Bold=""true"" FontSize=""14"" />
        </Field>
      </Fields>
    </Section>
    <Section Name=""Detail1"" Type=""Detail"" Height=""18"">
      <Fields />
    </Section>
    <Section Name=""PageFooter1"" Type=""PageFooter"" Height=""20"">
      <Fields>
        <Field Name=""PageNum"" Expression=""PageNumber()""
               X=""400"" Y=""4"" Width=""100"" Height=""12"" Alignment=""Right"" />
      </Fields>
    </Section>
  </Sections>
</Report>";

        private NewReportCommand(AsyncPackage package, OleMenuCommandService commandService)
        {
            _package = package;

            var cmdId = new CommandID(
                ReportDesigner.VsExtension.PackageGuids.guidReportDesignerPackageCmdSet,
                ReportDesigner.VsExtension.PackageCommandIds.NewReport);

            commandService.AddCommand(new OleMenuCommand(Execute, cmdId));
        }

        public static async Task InitializeAsync(AsyncPackage package)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(
                package.DisposalToken);

            var svc = await package.GetServiceAsync(typeof(IMenuCommandService))
                      as OleMenuCommandService;
            if (svc is not null)
                new NewReportCommand(package, svc);
        }

        private void Execute(object sender, EventArgs e)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            var dialog = new Microsoft.Win32.SaveFileDialog
            {
                Title      = "Create New Report",
                Filter     = "Report Definition (*.rdx)|*.rdx",
                FileName   = "NewReport.rdx",
                DefaultExt = ".rdx",
            };

            if (dialog.ShowDialog() != true) return;

            // Write blank template
            File.WriteAllText(dialog.FileName, _blankRdx, System.Text.Encoding.UTF8);

            // Open in designer
            OpenReportCommand.OpenRdxFile(dialog.FileName);
        }
    }
}
