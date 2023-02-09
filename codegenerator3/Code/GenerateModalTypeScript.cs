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
        public string GenerateModalTypeScript()
        {
            var folders = string.Join("", Enumerable.Repeat("../", CurrentEntity.Project.GeneratedPath.Count(o => o == '/')));

            var s = new StringBuilder();

            var file = File.ReadAllText(AppDomain.CurrentDomain.BaseDirectory + "templates/selectmodal.ts.txt");

            var filterParams = string.Empty;
            var inputs = string.Empty;
            var imports = string.Empty;
            var properties = string.Empty;
            var searchOptions = string.Empty;

            var lookups = CurrentEntity.Fields.Where(f => f.FieldType == FieldType.Enum && (f.SearchType == SearchType.Exact || f.ShowInSearchResults)).Select(f => f.Lookup).Distinct().OrderBy(o => o.Name);

            if (CurrentEntity.EntityType == EntityType.User)
            {
                if (!lookups.Any()) imports += $"import {{ Enum }} from '{folders}../common/models/enums.model';" + Environment.NewLine;
                inputs += $"    @Input() role: Enum;" + Environment.NewLine;
                searchOptions += $"        this.searchOptions.roleName = this.role ? this.role.name : undefined;" + Environment.NewLine;
            }

            if (lookups.Any())
                imports += $"import {{ Enum, Enums{(CurrentEntity.EntityType == EntityType.User ? "Role" : "")} }} from '{folders}../common/models/enums.model';" + Environment.NewLine;

            if (!lookups.Any() && CurrentEntity.Fields.Where(o => o.ShowInSearchResults && o.FieldType == FieldType.Enum).Any())
            {
                imports += $"import {{ Enums }} from '{folders}../common/models/enums.model';" + Environment.NewLine;
                foreach (var lookup in CurrentEntity.Fields.Where(o => o.ShowInSearchResults && o.FieldType == FieldType.Enum).Select(o => o.Lookup).Distinct())
                    properties += $"    {lookup.PluralName.ToCamelCase()} = Enums.{lookup.PluralName};" + Environment.NewLine;
            }

            foreach (var lookup in lookups)
            {
                properties += $"    {lookup.PluralName.ToCamelCase()} = Enums.{lookup.PluralName};" + Environment.NewLine;
                //properties += $"   {lookup.Name.ToCamelCase()}: Enum;" + Environment.NewLine;
            }

            var imported = new List<string>();
            foreach (var field in CurrentEntity.Fields.Where(o => o.SearchType == SearchType.Exact).OrderBy(f => f.FieldOrder))
            {
                Relationship relationship = null;
                if (CurrentEntity.RelationshipsAsChild.Any(r => r.RelationshipFields.Any(f => f.ChildFieldId == field.FieldId)))
                    relationship = CurrentEntity.GetParentSearchRelationship(field);

                if (field.FieldType == FieldType.Enum)
                    filterParams += $"{Environment.NewLine}                {field.Name.ToCamelCase()}: (options.{field.Name.ToCamelCase()} ? options.{field.Name.ToCamelCase()}.id : undefined),";
                else if (relationship != null)
                    filterParams += $"{Environment.NewLine}                {field.Name.ToCamelCase()}: (options.{relationship.ParentName.ToCamelCase()} ? options.{relationship.ParentName.ToCamelCase()}.{relationship.RelationshipFields.Single().ParentField.Name.ToCamelCase()} : undefined),";

                if (field.FieldType == FieldType.Enum)
                {
                    inputs += $"    @Input() {field.Name.ToCamelCase()}: Enum;" + Environment.NewLine;
                    searchOptions += $"        this.searchOptions.{field.Name.ToCamelCase()} = this.{field.Name.ToCamelCase()} ? this.{field.Name.ToCamelCase()}.value : undefined;" + Environment.NewLine;
                }
                else if (relationship != null)
                {
                    inputs += $"    @Input() {relationship.ParentName.ToCamelCase()}: {relationship.ParentEntity.Name};" + Environment.NewLine;
                    searchOptions += $"        this.searchOptions.{field.Name.ToCamelCase()} = this.{relationship.ParentName.ToCamelCase()} ? this.{relationship.ParentName.ToCamelCase()}.{relationship.ParentEntity.KeyFields.First().Name.ToCamelCase()} : undefined;" + Environment.NewLine;

                    if (relationship.ParentEntity != CurrentEntity && !imported.Contains(relationship.ParentEntity.Name))
                    {
                        imported.Add(relationship.ParentEntity.Name);
                        imports += $"import {{ {relationship.ParentEntity.Name} }} from '{folders}../common/models/{relationship.ParentEntity.Name.ToLower()}.model';" + Environment.NewLine;
                    }
                }
            }

            file = RunTemplateReplacements(file)
                .Replace("/*IMPORTS*/", imports)
                .Replace("/*FOLDERS*/", folders)
                .Replace("/*INPUTS*/", inputs)
                .Replace("/*PROPERTIES*/", properties)
                .Replace("/*SEARCHOPTIONS*/", searchOptions)
                .Replace("/*FILTER_PARAMS*/", filterParams);
            //.Replace("/*FILTER_TRIGGERS*/", filterTriggers);

            s.Add(file);

            return RunCodeReplacements(s.ToString(), CodeType.SelectModalTypeScript);
        }
    }
}