using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace WEB.Models
{
    public partial class Code
    {
        public string GenerateSearchOptions()
        {
            var s = new StringBuilder();

            var file = File.ReadAllText(AppDomain.CurrentDomain.BaseDirectory + "templates/searchoptions.txt");

            var searchFields = string.Empty;

            if (CurrentEntity.Fields.Any(o => o.SearchType == SearchType.Text))
                searchFields += $"        public string q {{ get; set; }}{Environment.NewLine}{Environment.NewLine}";

            foreach (var field in CurrentEntity.AllNonTextSearchableFields)
                searchFields += $"        public {Field.GetNetType(field.FieldType, true, field.Lookup)} {field.Name} {{ get; set; }}{Environment.NewLine}{Environment.NewLine}";

            s.Add(RunTemplateReplacements(file)
                .Replace("/*SEARCH-FIELDS*/", searchFields));

            return RunCodeReplacements(s.ToString(), CodeType.SearchOptions);
        }
    }
}