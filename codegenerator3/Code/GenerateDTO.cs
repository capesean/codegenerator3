using System.Collections.Generic;
using System.Linq;
using System.Text;

/*
 notes on error: Possibly unhandled rejection:
 as soon as you work with the .$promise, you have to include another .catch() 
 if you don't, then you get the error. 
     */

namespace WEB.Models
{
    public partial class Code
    {
        public string GenerateDTO()
        {
            var relationshipsAsChild = CurrentEntity.RelationshipsAsChild.OrderBy(o => o.ParentFriendlyName);
            var relationshipsAsParent = CurrentEntity.RelationshipsAsParent.OrderBy(o => o.ParentFriendlyName);

            var s = new StringBuilder();

            s.Add($"using System;");
            s.Add($"using System.ComponentModel.DataAnnotations;");
            if (CurrentEntity.EntityType == EntityType.User)
            {
                s.Add($"using System.Collections.Generic;");
                s.Add($"using System.Linq;");
            }
            s.Add($"");
            s.Add($"namespace {CurrentEntity.Project.Namespace}.Models");
            s.Add($"{{");
            s.Add($"    public class {CurrentEntity.DTOName}");
            s.Add($"    {{");
            foreach (var field in CurrentEntity.Fields.OrderBy(f => f.FieldOrder))
            {
                if (field.EditPageType == EditPageType.Exclude) continue;

                if (field.EditPageType == EditPageType.FileContents)
                {
                    s.Add($"        public string {field.Name} {{ get; set; }}");
                    s.Add($"");
                    continue;
                }

                var attributes = new List<string>();

                if (field.EditPageType != EditPageType.CalculatedField)
                {
                    if (!field.IsNullable)
                    {
                        // to allow empty strings, can't be null and must use convertemptystringtonull...
                        if (field.CustomType == CustomType.String)
                            attributes.Add("DisplayFormat(ConvertEmptyStringToNull = false)");
                        else if (field.EditPageType != EditPageType.ReadOnly)
                            attributes.Add("Required");
                    }

                    if (field.FieldType == FieldType.Colour)
                        attributes.Add($"MaxLength(7)");
                    else if (field.NetType == "string" && field.Length > 0)
                        attributes.Add($"MaxLength({field.Length}){(field.MinLength > 0 ? $", MinLength({field.MinLength})" : "")}");
                }

                if (attributes.Any())
                    s.Add($"        [{string.Join(", ", attributes)}]");

                // force nullable for readonly fields
                s.Add($"        public {Field.GetNetType(field.FieldType, field.EditPageType == EditPageType.ReadOnly ? true : field.IsNullable, field.Lookup)} {field.Name} {{ get; set; }}");
                s.Add($"");
            }
            // sort order on relationships is for parents. for childre, just use name
            foreach (var relationship in CurrentEntity.RelationshipsAsChild.Where(o => !o.ParentEntity.Exclude).OrderBy(r => r.ParentEntity.Name).ThenBy(o => o.ParentName))
            {
                // using exclude to avoid circular references. example: KTU-PACK: version => localisation => contentset => version (UpdateFromVersion)
                // changed: allow DTO model, so the value can come UP from the browser. example: RURA bankbalance - allow save of documents but not get/search
                // to resolve KTU issue, suggest not including?
                //if (relationship.RelationshipAncestorLimit == RelationshipAncestorLimits.Exclude) continue;
                if (relationship.RelationshipFields.Count == 1 && relationship.RelationshipFields.First().ChildField.EditPageType == EditPageType.Exclude) continue;
                s.Add($"        public {relationship.ParentEntity.Name}DTO {relationship.ParentName} {{ get; set; }}");
                s.Add($"");
            }

            foreach (var relationship in CurrentEntity.RelationshipsAsParent.Where(o => !o.ParentEntity.Exclude).OrderBy(r => r.ChildEntity.Name).ThenBy(o => o.CollectionName).ThenBy(o => o.RelationshipId))
            {
                s.Add($"        public virtual List<{relationship.ChildEntity.Name}DTO> {relationship.CollectionName} {{ get; set; }} = new List<{relationship.ChildEntity.Name}DTO>();");
                s.Add($"");
            }


            if (CurrentEntity.EntityType == EntityType.User)
            {
                s.Add($"        public IList<string> Roles {{ get; set; }}");
                s.Add($"");
            }
            s.Add($"    }}");
            s.Add($"");
            s.Add($"    public static partial class ModelFactory");
            s.Add($"    {{");
            s.Add($"        public static {CurrentEntity.DTOName} Create({CurrentEntity.Name} {CurrentEntity.CamelCaseName}, bool includeParents = true, bool includeChildren = false{(CurrentEntity.EntityType == EntityType.User ? ", List<Role> dbRoles = null" : "")})");
            s.Add($"        {{");
            s.Add($"            if ({CurrentEntity.CamelCaseName} == null) return null;");
            s.Add($"");
            if (CurrentEntity.EntityType == EntityType.User)
            {
                s.Add($"            var userRoles = new List<string>();");
                s.Add($"            if ({CurrentEntity.CamelCaseName}.Roles != null && dbRoles != null)");
                s.Add($"                userRoles = dbRoles.Where(o => user.Roles.Any(r => r.RoleId == o.Id)).Select(o => o.Name).ToList();");
                s.Add($"");
            }
            s.Add($"            var {CurrentEntity.DTOName.ToCamelCase()} = new {CurrentEntity.DTOName}();");
            s.Add($"");
            foreach (var field in CurrentEntity.Fields.Where(f => f.EditPageType != EditPageType.Exclude && f.EditPageType != EditPageType.EditOnly && f.EditPageType != EditPageType.FileContents).OrderBy(f => f.FieldOrder))
            {
                s.Add($"            {CurrentEntity.DTOName.ToCamelCase()}.{field.Name} = {CurrentEntity.CamelCaseName}.{field.Name};");
            }
            if (CurrentEntity.EntityType == EntityType.User)
            {
                s.Add($"            {CurrentEntity.DTOName.ToCamelCase()}.Roles = userRoles;");
            }

            if (relationshipsAsChild.Any())
            {
                s.Add($"");
                s.Add($"            if (includeParents)");
                s.Add($"            {{");
                foreach (var relationship in relationshipsAsChild)
                {
                    // using exclude to avoid circular references. example: KTU-PACK: version => localisation => contentset => version (UpdateFromVersion)
                    if (relationship.RelationshipAncestorLimit == RelationshipAncestorLimits.Exclude) continue;
                    if (relationship.RelationshipFields.Count == 1 && relationship.RelationshipFields.First().ChildField.EditPageType == EditPageType.Exclude) continue;
                    s.Add($"                {CurrentEntity.DTOName.ToCamelCase()}.{relationship.ParentName} = Create({CurrentEntity.CamelCaseName}.{relationship.ParentName});");
                }
                s.Add($"            }}");
                s.Add($"");
            }

            if (relationshipsAsParent.Any())
            {
                s.Add($"            if (includeChildren)");
                s.Add($"            {{");
                foreach (var relationship in relationshipsAsParent)
                {
                    if (relationship.RelationshipFields.Count == 1 && relationship.RelationshipFields.First().ChildField.EditPageType == EditPageType.Exclude) continue;
                    
                    s.Add($"                foreach (var {relationship.CollectionSingular.ToCamelCase()} in {CurrentEntity.CamelCaseName}.{relationship.CollectionName})");
                    s.Add($"                    {CurrentEntity.DTOName.ToCamelCase()}.{relationship.CollectionName}.Add(Create({relationship.CollectionSingular.ToCamelCase()}));");
                }
                s.Add($"            }}");
                s.Add($"");
            }


            s.Add($"            return {CurrentEntity.DTOName.ToCamelCase()};");
            s.Add($"        }}");
            s.Add($"");
            s.Add($"        public static void Hydrate({CurrentEntity.Name} {CurrentEntity.CamelCaseName}, {CurrentEntity.DTOName} {CurrentEntity.DTOName.ToCamelCase()})");
            s.Add($"        {{");
            if (CurrentEntity.EntityType == EntityType.User)
            {
                s.Add($"            {CurrentEntity.CamelCaseName}.UserName = {CurrentEntity.DTOName.ToCamelCase()}.Email;");
            }
            foreach (var field in CurrentEntity.Fields.OrderBy(f => f.FieldOrder))
            {
                if (field.KeyField || field.EditPageType == EditPageType.ReadOnly) continue;
                if (field.EditPageType == EditPageType.Exclude || field.EditPageType == EditPageType.CalculatedField) continue;
                if (field.EditPageType == EditPageType.FileContents)
                    s.Add($"            if ({CurrentEntity.DTOName.ToCamelCase()}.{field.Name} != null) {CurrentEntity.CamelCaseName}.{field.Name} = Convert.FromBase64String({CurrentEntity.DTOName.ToCamelCase()}.{field.Name});");
                else
                    s.Add($"            {CurrentEntity.CamelCaseName}.{field.Name} = {CurrentEntity.DTOName.ToCamelCase()}.{field.Name};");
            }
            s.Add($"        }}");
            s.Add($"    }}");
            s.Add($"}}");

            return RunCodeReplacements(s.ToString(), CodeType.DTO);
        }
    }
}