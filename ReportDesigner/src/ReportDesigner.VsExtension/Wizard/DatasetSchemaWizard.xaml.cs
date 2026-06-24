using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Xml.Linq;

namespace ReportDesigner.VsExtension.Wizard
{
    /// <summary>
    /// Code-behind for the Dataset Schema Editor dialog.
    /// Lets the user add, remove, reorder, and type fields in a .rds file,
    /// and manage sample data rows for design-time preview.
    /// </summary>
    public partial class DatasetSchemaWizard : Window
    {
        // ── Bindable item ─────────────────────────────────────────────

        public class FieldItem : INotifyPropertyChanged
        {
            private string _name        = string.Empty;
            private string _dataType    = "String";
            private string _caption     = string.Empty;
            private bool   _nullable    = true;
            private string _sampleValue = string.Empty;

            public string Name
            {
                get => _name;
                set { _name = value; OnProp(); }
            }
            public string DataType
            {
                get => _dataType;
                set { _dataType = value; OnProp(); }
            }
            public string Caption
            {
                get => _caption;
                set { _caption = value; OnProp(); }
            }
            public bool Nullable
            {
                get => _nullable;
                set { _nullable = value; OnProp(); }
            }
            public string SampleValue
            {
                get => _sampleValue;
                set { _sampleValue = value; OnProp(); }
            }

            public event PropertyChangedEventHandler PropertyChanged;
            private void OnProp([System.Runtime.CompilerServices.CallerMemberName] string n = "")
                => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
        }

        // ── State ──────────────────────────────────────────────────────

        private readonly string                  _filePath;
        private readonly ObservableCollection<FieldItem> _fields = new();
        private          List<Dictionary<string, string>> _sampleRows = new();

        /// <summary>The serialised .rds XML produced on Save. Null if user cancelled.</summary>
        public string? ResultXml { get; private set; }

        /// <summary>All available data type names (bound to ComboBox in the grid).</summary>
        public IEnumerable<string> DataTypes { get; } = new[]
        {
            "String", "Int32", "Int64", "Decimal", "Double",
            "Float", "Boolean", "DateTime", "Guid", "Byte"
        };

        // ── Constructor ────────────────────────────────────────────────

        public DatasetSchemaWizard(string filePath, string existingXml)
        {
            InitializeComponent();
            DataContext   = this;
            _filePath     = filePath;

            FilePathLabel.Text = filePath;
            FieldGrid.ItemsSource = _fields;

            if (!string.IsNullOrWhiteSpace(existingXml))
                ParseExistingSchema(existingXml);
            else
                SchemaNameBox.Text = Path.GetFileNameWithoutExtension(filePath);

            UpdateSampleRowCount();
        }

        // ── Parse existing .rds ────────────────────────────────────────

        private void ParseExistingSchema(string xml)
        {
            try
            {
                var ns  = XNamespace.Get("urn:reportdesigner");
                var doc = XDocument.Parse(xml);
                var root = doc.Root;

                SchemaNameBox.Text = root?.Attribute("Name")?.Value
                                     ?? root?.Attribute("name")?.Value
                                     ?? Path.GetFileNameWithoutExtension(_filePath);

                // Fields
                var fieldsEl = root?.Element(ns + "Fields") ?? root?.Element("Fields");
                if (fieldsEl is not null)
                {
                    foreach (var f in fieldsEl.Elements())
                    {
                        _fields.Add(new FieldItem
                        {
                            Name        = f.Attribute("Name")?.Value   ?? f.Attribute("name")?.Value   ?? string.Empty,
                            DataType    = f.Attribute("DataType")?.Value?? f.Attribute("type")?.Value   ?? "String",
                            Caption     = f.Attribute("Caption")?.Value ?? f.Attribute("caption")?.Value ?? string.Empty,
                            Nullable    = f.Attribute("Nullable")?.Value?.ToLower() != "false",
                        });
                    }
                }

                // Sample data
                var sampleEl = root?.Element(ns + "SampleData") ?? root?.Element("SampleData");
                if (sampleEl is not null)
                {
                    foreach (var row in sampleEl.Elements())
                    {
                        var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                        foreach (var attr in row.Attributes())
                            dict[attr.Name.LocalName] = attr.Value;
                        _sampleRows.Add(dict);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Could not parse existing schema:\n{ex.Message}",
                    "Schema Editor", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        // ── Field grid toolbar ─────────────────────────────────────────

        private void AddField_Click(object sender, RoutedEventArgs e)
        {
            var item = new FieldItem
            {
                Name    = $"Field{_fields.Count + 1}",
                DataType = "String",
                Nullable = true,
            };
            _fields.Add(item);
            FieldGrid.SelectedItem = item;
            FieldGrid.ScrollIntoView(item);
            FieldGrid.BeginEdit();
        }

        private void RemoveField_Click(object sender, RoutedEventArgs e)
        {
            if (FieldGrid.SelectedItem is FieldItem item)
                _fields.Remove(item);
        }

        private void MoveUp_Click(object sender, RoutedEventArgs e)
        {
            var idx = FieldGrid.SelectedIndex;
            if (idx <= 0) return;
            _fields.Move(idx, idx - 1);
            FieldGrid.SelectedIndex = idx - 1;
        }

        private void MoveDown_Click(object sender, RoutedEventArgs e)
        {
            var idx = FieldGrid.SelectedIndex;
            if (idx < 0 || idx >= _fields.Count - 1) return;
            _fields.Move(idx, idx + 1);
            FieldGrid.SelectedIndex = idx + 1;
        }

        private void FieldGrid_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            var hasSelection = FieldGrid.SelectedItem is not null;
            MoveUpBtn.IsEnabled   = hasSelection && FieldGrid.SelectedIndex > 0;
            MoveDownBtn.IsEnabled = hasSelection && FieldGrid.SelectedIndex < _fields.Count - 1;
            RemoveBtn.IsEnabled   = hasSelection;
        }

        // ── Import helpers ─────────────────────────────────────────────

        private void ImportSql_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new SqlImportDialog { Owner = this };
            if (dlg.ShowDialog() == true && dlg.ParsedFields is not null)
            {
                foreach (var (name, type) in dlg.ParsedFields)
                {
                    _fields.Add(new FieldItem { Name = name, DataType = type });
                }
            }
        }

        private void ImportCsv_Click(object sender, RoutedEventArgs e)
        {
            var openDlg = new Microsoft.Win32.OpenFileDialog
            {
                Title  = "Select CSV file",
                Filter = "CSV Files (*.csv)|*.csv|All Files|*.*",
            };
            if (openDlg.ShowDialog() != true) return;

            try
            {
                var header = File.ReadLines(openDlg.FileName).FirstOrDefault();
                if (string.IsNullOrWhiteSpace(header)) return;

                foreach (var col in header.Split(','))
                {
                    var name = col.Trim().Trim('"');
                    if (!string.IsNullOrWhiteSpace(name))
                        _fields.Add(new FieldItem { Name = name, DataType = "String" });
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Import failed:\n{ex.Message}", "Schema Editor",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        // ── Sample data ────────────────────────────────────────────────

        private void EditSampleData_Click(object sender, RoutedEventArgs e)
        {
            FieldGrid.CommitEdit(System.Windows.Controls.DataGridEditingUnit.Row, true);

            var fieldNames = _fields.Select(f => f.Name).ToList();
            if (!fieldNames.Any())
            {
                MessageBox.Show("Add at least one field before editing sample data.",
                    "Schema Editor", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var dlg = new SampleDataDialog(fieldNames, _sampleRows) { Owner = this };
            if (dlg.ShowDialog() == true)
            {
                _sampleRows = dlg.Rows;
                UpdateSampleRowCount();
            }
        }

        private void UpdateSampleRowCount()
            => SampleRowCountLabel.Text = $"{_sampleRows.Count} row(s)";

        // ── Save ───────────────────────────────────────────────────────

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            FieldGrid.CommitEdit(System.Windows.Controls.DataGridEditingUnit.Row, true);

            var name = SchemaNameBox.Text.Trim();
            if (string.IsNullOrWhiteSpace(name))
            {
                MessageBox.Show("Please enter a dataset name.", "Schema Editor",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            ResultXml  = BuildXml(name);
            DialogResult = true;
        }

        private string BuildXml(string name)
        {
            var sb = new StringBuilder();
            sb.AppendLine("<?xml version=\"1.0\" encoding=\"utf-8\"?>");
            sb.AppendLine($"<DataSet xmlns=\"urn:reportdesigner\" Name=\"{Escape(name)}\">");

            sb.AppendLine("  <Fields>");
            foreach (var f in _fields)
            {
                sb.Append($"    <Field Name=\"{Escape(f.Name)}\"");
                sb.Append($" DataType=\"{Escape(f.DataType)}\"");
                if (!string.IsNullOrWhiteSpace(f.Caption))
                    sb.Append($" Caption=\"{Escape(f.Caption)}\"");
                sb.Append($" Nullable=\"{f.Nullable.ToString().ToLower()}\"");
                sb.AppendLine(" />");
            }
            sb.AppendLine("  </Fields>");

            sb.AppendLine("  <SampleData>");
            foreach (var row in _sampleRows)
            {
                sb.Append("    <Row");
                foreach (var kv in row)
                    sb.Append($" {Escape(kv.Key)}=\"{Escape(kv.Value)}\"");
                sb.AppendLine(" />");
            }
            sb.AppendLine("  </SampleData>");

            sb.AppendLine("</DataSet>");
            return sb.ToString();
        }

        private static string Escape(string s)
            => s?.Replace("&", "&amp;").Replace("\"", "&quot;")
                 .Replace("<", "&lt;").Replace(">", "&gt;") ?? string.Empty;
    }
}
