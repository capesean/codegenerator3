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
        public string GenerateSelectTypeScript()
        {
            var folders = string.Join("", Enumerable.Repeat("../", CurrentEntity.Project.GeneratedPath.Count(o => o == '/')));

            var s = new StringBuilder();

            var file = File.ReadAllText(AppDomain.CurrentDomain.BaseDirectory + "templates/appselect.ts.txt");

            var filterAttributes = string.Empty;
            var filterOptions = string.Empty;
            var inputs = string.Empty;
            var imports = string.Empty;

            if (CurrentEntity.EntityType == EntityType.User)
            {
                inputs += $"    @Input() role: Enum;" + Environment.NewLine;
            }

            var imported = new List<string>();
            foreach (var field in CurrentEntity.Fields.Where(o => o.SearchType == SearchType.Exact && (o.FieldType == FieldType.Enum || o.FieldType == FieldType.Bit || CurrentEntity.RelationshipsAsChild.Any(r => r.RelationshipFields.Any(f => f.ChildFieldId == o.FieldId)))).OrderBy(f => f.FieldOrder))
            {
                var name = field.Name.ToCamelCase();

                Relationship relationship = null;
                if (CurrentEntity.RelationshipsAsChild.Any(r => r.RelationshipFields.Any(f => f.ChildFieldId == field.FieldId)))
                {
                    relationship = CurrentEntity.GetParentSearchRelationship(field);
                    name = relationship.ParentName.ToCamelCase();
                }

                filterAttributes += $",{Environment.NewLine}                {name}: \"<\"";
                filterOptions += $",{Environment.NewLine}                            {name}: $scope.{name}";

                if (field.FieldType == FieldType.Enum)
                    inputs += $"    @Input() {field.Name.ToCamelCase()}: Enum;" + Environment.NewLine;
                else if (field.FieldType == FieldType.Bit && (CurrentEntity.EntityType != EntityType.User || field.Name != "Disabled"))
                    inputs += $"    @Input() {field.Name.ToCamelCase()}: boolean;" + Environment.NewLine;
                else if (relationship != null)
                {
                    inputs += $"    @Input() {relationship.ParentName.ToCamelCase()}: {relationship.ParentEntity.Name};" + Environment.NewLine;
                    if (relationship.ParentEntity != CurrentEntity && !imported.Contains(relationship.ParentEntity.Name))
                    {
                        imported.Add(relationship.ParentEntity.Name);
                        imports += $"import {{ {relationship.ParentEntity.Name} }} from '{folders}../common/models/{relationship.ParentEntity.Name.ToLower()}.model';" + Environment.NewLine;
                    }
                }
            }

            if (CurrentEntity.PrimaryField == null) throw new Exception("Entity " + CurrentEntity.Name + " does not have a Primary Field defined for AppSelect label");

            var LABEL_OUTPUT_MULTI = $"{CurrentEntity.CamelCaseName}.{CurrentEntity.PrimaryField.Name.ToCamelCase()}";
            var LABEL_OUTPUT_SINGLE = $"this.{CurrentEntity.CamelCaseName}?.{CurrentEntity.PrimaryField.Name.ToCamelCase()}";

            if (CurrentEntity.PrimaryField.CustomType == CustomType.Date)
            {
                imports += $"import * as moment from 'moment';" + Environment.NewLine;
                LABEL_OUTPUT_MULTI = $"({LABEL_OUTPUT_MULTI} ? moment({LABEL_OUTPUT_MULTI}).format(\"LL\") : undefined)";
                LABEL_OUTPUT_SINGLE = $"({LABEL_OUTPUT_SINGLE} ? moment({LABEL_OUTPUT_SINGLE}).format(\"LL\") : undefined)";
            }
            else if (CurrentEntity.PrimaryField.FieldType == FieldType.Enum)
            {
                LABEL_OUTPUT_MULTI = $"Enums.{CurrentEntity.PrimaryField.Lookup.PluralName}[{LABEL_OUTPUT_MULTI}]?.label";
                LABEL_OUTPUT_SINGLE = $"Enums.{CurrentEntity.PrimaryField.Lookup.PluralName}[{LABEL_OUTPUT_SINGLE}]?.label";
            }

            var enums = CurrentEntity.PrimaryField.FieldType == FieldType.Enum ? ", Enums" : string.Empty;

            file = RunTemplateReplacements(file)
                .Replace("/*FOLDERS*/", folders)
                .Replace("/*FILTER_ATTRIBUTES*/", filterAttributes)
                .Replace("/*FILTER_OPTIONS*/", filterOptions)
                .Replace("/*INPUTS*/", inputs)
                .Replace("/*ENUMS*/", enums)
                .Replace("/*IMPORTS*/", imports)
                .Replace("LABEL_OUTPUT_MULTI", LABEL_OUTPUT_MULTI)
                .Replace("LABEL_OUTPUT_SINGLE", LABEL_OUTPUT_SINGLE)
                .Replace("LABELFIELD", CurrentEntity.PrimaryField.Name.ToCamelCase());

            s.Add(file);

            return RunCodeReplacements(s.ToString(), CodeType.AppSelectTypeScript);
        }
    }
}