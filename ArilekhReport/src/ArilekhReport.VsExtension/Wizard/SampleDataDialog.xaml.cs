using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace ReportDesigner.VsExtension.Wizard
{
    public partial class SampleDataDialog : Window
    {
        private readonly List<string> _fieldNames;
        private readonly DataTable    _table;

        /// <summary>The edited rows, returned to the caller on OK.</summary>
        public List<Dictionary<string, string>> Rows { get; private set; } = new();

        public SampleDataDialog(
            List<string> fieldNames,
            List<Dictionary<string, string>> existingRows)
        {
            InitializeComponent();
            _fieldNames = fieldNames;

            // Build DataTable – one column per field
            _table = new DataTable();
            foreach (var name in fieldNames)
                _table.Columns.Add(name, typeof(string));

            // Load existing rows
            foreach (var dict in existingRows)
            {
                var row = _table.NewRow();
                foreach (var name in fieldNames)
                    row[name] = dict.TryGetValue(name, out var v) ? v : string.Empty;
                _table.Rows.Add(row);
            }

            // Build DataGrid columns dynamically
            foreach (var name in fieldNames)
            {
                SampleGrid.Columns.Add(new DataGridTextColumn
                {
                    Header  = name,
                    Binding = new System.Windows.Data.Binding($"[{name}]"),
                    Width   = new DataGridLength(1, DataGridLengthUnitType.Star),
                });
            }

            SampleGrid.ItemsSource = _table.DefaultView;
        }

        private void AddRow_Click(object sender, RoutedEventArgs e)
        {
            var row = _table.NewRow();
            foreach (var name in _fieldNames) row[name] = string.Empty;
            _table.Rows.Add(row);
            if (SampleGrid.Items.Count > 0)
                SampleGrid.ScrollIntoView(SampleGrid.Items[SampleGrid.Items.Count - 1]);
        }

        private void RemoveRow_Click(object sender, RoutedEventArgs e)
        {
            if (SampleGrid.SelectedIndex < 0) return;
            _table.Rows.RemoveAt(SampleGrid.SelectedIndex);
        }

        private void Ok_Click(object sender, RoutedEventArgs e)
        {
            // Commit any in-progress edits
            SampleGrid.CommitEdit(DataGridEditingUnit.Row, true);

            Rows = _table.Rows
                .Cast<DataRow>()
                .Select(r => _fieldNames.ToDictionary(
                    n => n,
                    n => r[n]?.ToString() ?? string.Empty))
                .ToList();

            DialogResult = true;
        }
    }
}
