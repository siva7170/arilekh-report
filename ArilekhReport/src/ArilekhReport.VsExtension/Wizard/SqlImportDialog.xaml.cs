using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Windows;

namespace ReportDesigner.VsExtension.Wizard
{
    public partial class SqlImportDialog : Window
    {
        /// <summary>Parsed (name, rdType) pairs returned to the caller.</summary>
        public List<(string Name, string Type)>? ParsedFields { get; private set; }

        public SqlImportDialog() => InitializeComponent();

        private void Import_Click(object sender, RoutedEventArgs e)
        {
            ErrorLabel.Visibility = Visibility.Collapsed;
            var sql = SqlBox.Text?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(sql))
            {
                ShowError("Please paste a SQL statement or column list.");
                return;
            }

            try
            {
                ParsedFields = ParseSql(sql);
                if (ParsedFields.Count == 0)
                {
                    ShowError("No columns detected. Check your syntax and try again.");
                    return;
                }
                DialogResult = true;
            }
            catch (Exception ex)
            {
                ShowError($"Parse error: {ex.Message}");
            }
        }

        private static List<(string, string)> ParseSql(string sql)
        {
            var results = new List<(string, string)>();

            // Strip CREATE TABLE xxx ( ... ) wrapper if present
            var match = Regex.Match(sql,
                @"CREATE\s+TABLE\s+\S+\s*\((.+)\)",
                RegexOptions.IgnoreCase | RegexOptions.Singleline);

            var body = match.Success ? match.Groups[1].Value : sql;

            // Split on commas (respect brackets)
            var columns = SplitColumns(body);

            foreach (var col in columns)
            {
                var colTrim = col.Trim()
                    .TrimStart('[').Replace("]", "")  // SQL Server brackets
                    .TrimStart('`').Replace("`", ""); // MySQL backticks

                if (string.IsNullOrWhiteSpace(colTrim)) continue;

                // Skip constraint keywords
                if (Regex.IsMatch(colTrim,
                    @"^(PRIMARY|FOREIGN|UNIQUE|INDEX|KEY|CONSTRAINT|CHECK)\b",
                    RegexOptions.IgnoreCase)) continue;

                // First token = column name, second = SQL type
                var parts = Regex.Split(colTrim.Trim(), @"\s+");
                if (parts.Length < 1) continue;

                var name    = parts[0].Trim();
                var sqlType = parts.Length > 1 ? parts[1].Trim() : "NVARCHAR";
                var rdType  = MapSqlType(sqlType);

                if (!string.IsNullOrWhiteSpace(name))
                    results.Add((name, rdType));
            }

            return results;
        }

        private static List<string> SplitColumns(string body)
        {
            var cols  = new List<string>();
            var depth = 0;
            var start = 0;

            for (int i = 0; i < body.Length; i++)
            {
                if (body[i] == '(') depth++;
                else if (body[i] == ')') depth--;
                else if (body[i] == ',' && depth == 0)
                {
                    cols.Add(body.Substring(start, i - start));
                    start = i + 1;
                }
            }
            if (start < body.Length)
                cols.Add(body.Substring(start));

            return cols;
        }

        private static string MapSqlType(string sqlType)
        {
            var t = sqlType.ToUpperInvariant().Split('(')[0].Trim();
            return t switch
            {
                "INT" or "INTEGER" or "SMALLINT" or "TINYINT" => "Int32",
                "BIGINT"                                       => "Int64",
                "DECIMAL" or "NUMERIC" or "MONEY"
                    or "SMALLMONEY"                            => "Decimal",
                "FLOAT" or "REAL"                             => "Double",
                "BIT"                                         => "Boolean",
                "DATETIME" or "DATETIME2" or "DATE"
                    or "SMALLDATETIME" or "TIMESTAMP"         => "DateTime",
                "UNIQUEIDENTIFIER"                            => "Guid",
                _                                             => "String",
            };
        }

        private void ShowError(string msg)
        {
            ErrorLabel.Text       = msg;
            ErrorLabel.Visibility = Visibility.Visible;
        }
    }
}
