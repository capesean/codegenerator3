using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Data.Entity;
using System.IO;

namespace WEB.Models
{
    public partial class Code
    {
        public string GenerateSortHtml()
        {
            var s = new StringBuilder();

            var file = File.ReadAllText(AppDomain.CurrentDomain.BaseDirectory + "templates/sort.html");

            var COLUMN_HEADERS = string.Empty;
            var COLUMNS = string.Empty;

            foreach (var field in CurrentEntity.Fields.Where(f => f.ShowInSearchResults).OrderBy(f => f.FieldOrder))
            {
                COLUMN_HEADERS += $"                <th>{field.Label}</th>{Environment.NewLine}";
                COLUMNS += $"                <td>{field.ListFieldHtml}</td>{Environment.NewLine}";
            }

            file = RunTemplateReplacements(file)
                .Replace("COLUMN_HEADERS", COLUMN_HEADERS)
                .Replace("COLUMNS", COLUMNS);

            s.Add(file);

            return RunCodeReplacements(s.ToString(), CodeType.SortHtml);
        }
    }
}