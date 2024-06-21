using System;
using System.Linq;
using System.Text;
using System.IO;

namespace WEB.Models
{
    public partial class Code
    {
        public string GenerateSelectHtml()
        {
            var s = new StringBuilder();

            var file = File.ReadAllText(AppDomain.CurrentDomain.BaseDirectory + "templates/appselect.html");

            var filterFields = string.Empty;

            if (CurrentEntity.EntityType == EntityType.User)
                filterFields += $" [role]=\"role\"";

            foreach (var field in CurrentEntity.Fields.Where(f => f.SearchType == SearchType.Exact).OrderBy(f => f.FieldOrder))
            {
                Relationship relationship = null;
                if (CurrentEntity.RelationshipsAsChild.Any(r => r.RelationshipFields.Any(rf => rf.ChildFieldId == field.FieldId)))
                    relationship = CurrentEntity.GetParentSearchRelationship(field);

                if (field.FieldType == FieldType.Enum || relationship != null)
                {
                    if (field.FieldType == FieldType.Enum)
                        filterFields += $" [{field.Name.ToCamelCase()}]=\"{field.Name.ToCamelCase()}\"";
                    else
                        filterFields += $" [{relationship.ParentName.ToCamelCase()}]=\"{relationship.ParentName.ToCamelCase()}\"";
                }
                else if (field.FieldType == FieldType.Bit)
                {
                    filterFields += $" [{field.Name.ToCamelCase()}]=\"{field.Name.ToCamelCase()}\"";
                }
            }
            s.Add(RunTemplateReplacements(file)
                .Replace("/*FILTER-FIELDS*/", filterFields));

            return RunCodeReplacements(s.ToString(), CodeType.AppSelectHtml);
        }
    }
}