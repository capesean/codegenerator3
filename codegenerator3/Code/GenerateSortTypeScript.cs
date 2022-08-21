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
        public string GenerateSortTypeScript()
        {
            var s = new StringBuilder();

            var file = File.ReadAllText(AppDomain.CurrentDomain.BaseDirectory + "templates/sort.txt");

            var enumLookups = CurrentEntity.Fields.Where(o => o.FieldType == FieldType.Enum && (o.ShowInSearchResults || o.SearchType == SearchType.Exact)).Select(o => o.Lookup).Distinct().ToList();

            var ENUM_PROPERTIES = string.Empty;
            foreach (var enumLookup in enumLookups)
                ENUM_PROPERTIES += $"    public {enumLookup.PluralName.ToCamelCase()}: Enum[] = Enums.{enumLookup.PluralName};{Environment.NewLine}";

            var PARENTPKFIELDS = string.Empty;
            var SEARCH_PARAMS = string.Empty;
            var hierarchyRel = CurrentEntity.RelationshipsAsChild.FirstOrDefault(o => o.Hierarchy);
            if (hierarchyRel != null)
            {
                foreach (var field in hierarchyRel.ParentEntity.KeyFields)
                {
                    PARENTPKFIELDS += $"    public {field.Name.ToCamelCase()}: string;{Environment.NewLine}";
                    SEARCH_PARAMS += $"{field.Name.ToCamelCase()}: this.{field.Name.ToCamelCase()}";
                }
                SEARCH_PARAMS += ", ";
            }

            file = RunTemplateReplacements(file)
                .Replace("ENUM_IMPORTS", enumLookups.Any() ? $"import {{ Enum, Enums }} from '../common/models/enums.model';{Environment.NewLine}" : "")
                .Replace("ENUM_PROPERTIES", enumLookups.Any() ? ENUM_PROPERTIES : string.Empty)
                .Replace("PARENTPKFIELDS", PARENTPKFIELDS)
                .Replace("SEARCH_PARAMS", SEARCH_PARAMS);

            s.Add(file);

            return RunCodeReplacements(s.ToString(), CodeType.SortTypeScript);
        }
    }
}