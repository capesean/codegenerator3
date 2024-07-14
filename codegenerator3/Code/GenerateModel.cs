using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
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
        public string GenerateModel()
        {
            if (CurrentEntity.EntityType == EntityType.User)
            {
                if (!CurrentEntity.Fields.Any(o => o.Name == "Id")) throw new Exception("An entity of Type: User must have a field named Id");
                if (!CurrentEntity.Fields.Any(o => o.Name == "Id" && o.KeyField)) throw new Exception("An entity of Type: User must have a field named Id that is a Primary Key");
                if (!CurrentEntity.Fields.Any(o => o.Name == "Email")) throw new Exception("An entity of Type: User must have a field named Email");
            }

            var fileContentsFields = CurrentEntity.Fields.Where(o => o.EditPageType == EditPageType.FileContents).ToList();
            if (fileContentsFields.Count > 1) throw new NotImplementedException("More than one File Contents field per entity");
            var fileContentsField = fileContentsFields.FirstOrDefault();

            var s = new StringBuilder();
            s.Add($"using System;");
            if (CurrentEntity.RelationshipsAsParent.Any(o => !o.ChildEntity.Exclude))
                s.Add($"using System.Collections.Generic;");
            s.Add($"using System.ComponentModel.DataAnnotations;");
            s.Add($"using System.ComponentModel.DataAnnotations.Schema;"); // decimals
            s.Add($"");
            s.Add($"namespace {CurrentEntity.Project.Namespace}.Models");
            s.Add($"{{");
            s.Add($"    public {(CurrentEntity.PartialEntityClass ? "partial " : string.Empty)}class {CurrentEntity.Name}");
            s.Add($"    {{");

            // fields
            var keyCounter = 0;
            foreach (var field in CurrentEntity.Fields.OrderBy(f => f.FieldOrder))
            {
                if (field.KeyField && CurrentEntity.EntityType == EntityType.User) continue;
                if (field.Name == "Email" && CurrentEntity.EntityType == EntityType.User) continue;

                var attributes = new List<string>();

                if (field.KeyField && CurrentEntity.KeyFields.Count == 1)
                {
                    attributes.Add("Key");
                    // probably shouldn't include decimals etc...
                    if (CurrentEntity.KeyFields.Count == 1 && field.CustomType == CustomType.Number)
                        attributes.Add("DatabaseGenerated(DatabaseGeneratedOption.Identity)");
                    if (CurrentEntity.KeyFields.Count > 1)
                        attributes.Add($"Column(Order = {keyCounter})");

                    keyCounter++;
                }

                if (field.EditPageType == EditPageType.CalculatedField)
                    attributes.Add("DatabaseGenerated(DatabaseGeneratedOption.Computed)");
                if (field.EditPageType == EditPageType.CalculatedField && field.FieldType == FieldType.Decimal)
                    attributes.Add($"Column(TypeName=\"decimal({field.Precision},{field.Scale})\")");
                else if (field.EditPageType == EditPageType.FileContents)
                {
                    if (!field.UseAzureBlobStorage)
                    {
                        s.Add($"        [Required]");
                        s.Add($"        public virtual {CurrentEntity.Name}Content {CurrentEntity.Name}Content {{ get; set; }}");
                        s.Add($"");
                    }
                    continue;
                }
                else
                {
                    if (!field.IsNullable)
                    {
                        if (field.CustomType == CustomType.String)
                            attributes.Add("Required(AllowEmptyStrings = true)");
                        else
                            attributes.Add("Required");
                    }

                    if (field.NetType == "string")
                    {
                        if (field.Length == 0 && (field.FieldType == FieldType.Varchar || field.FieldType == FieldType.nVarchar))
                        {
                            //?
                        }
                        else if (field.FieldType == FieldType.Colour)
                        {
                            attributes.Add($"MaxLength(7)");
                        }
                        else if (field.Length > 0)
                        {
                            attributes.Add($"MaxLength({field.Length}){(field.MinLength > 0 ? $", MinLength({field.MinLength})" : "")}");
                        }
                    }
                    else if (field.FieldType == FieldType.Date)
                        attributes.Add($"Column(TypeName = \"Date\")");
                    else if (field.NetType.StartsWith("decimal") && field.EditPageType != EditPageType.CalculatedField)
                        attributes.Add($"Column(TypeName = \"decimal({field.Precision}, {field.Scale})\")");
                }

                if (attributes.Count > 0)
                    s.Add($"        [" + string.Join(", ", attributes) + "]");

                if (field.EditPageType == EditPageType.CalculatedField)
                {
                    s.Add($"        public {field.NetType} {field.Name} {{ get; private set; }}");
                }
                else
                {
                    s.Add($"        public {field.NetType} {field.Name} {{ get; set; }}");
                }
                s.Add($"");
            }

            // child entities
            foreach (var relationship in CurrentEntity.RelationshipsAsParent.Where(r => !r.ChildEntity.Exclude && !r.ParentEntity.Exclude).OrderBy(o => o.SortOrder))
            {
                if (!relationship.IsOneToOne)
                    s.Add($"        public virtual ICollection<{GetEntity(relationship.ChildEntityId).Name}> {relationship.CollectionName} {{ get; set; }} = new List<{GetEntity(relationship.ChildEntityId).Name}>();");
                else
                    s.Add($"        public {GetEntity(relationship.ChildEntityId).Name} {relationship.CollectionSingular} {{ get; set; }}");
                s.Add($"");
            }

            // parent entities
            foreach (var relationship in CurrentEntity.RelationshipsAsChild.Where(r => !r.ChildEntity.Exclude && !r.ParentEntity.Exclude).OrderBy(o => o.ParentEntity.Name).ThenBy(o => o.CollectionName))
            {
                if (relationship.RelationshipFields.Count() == 1)
                    s.Add($"        [ForeignKey(\"" + relationship.RelationshipFields.Single().ChildField.Name + "\")]");
                s.Add($"        public virtual {GetEntity(relationship.ParentEntityId).Name} {relationship.ParentName} {{ get; set; }}");
                s.Add($"");
            }

            // constructor
            if (CurrentEntity.KeyFields.Any(f => f.KeyField && f.FieldType == FieldType.Guid))
            {
                s.Add($"        public {CurrentEntity.Name}()");
                s.Add($"        {{");
                foreach (var field in CurrentEntity.KeyFields)
                {
                    // where the primary key is a composite with the guid being a fk, don't init the field. e.g. IXESHA.ConsultantMonths.ConsultantId (+Year+Month)
                    if (CurrentEntity.KeyFields.Count > 1 && CurrentEntity.RelationshipsAsChild.Any(r => r.RelationshipFields.Any(rf => rf.ChildFieldId == field.FieldId)))
                        continue;
                    if (field.FieldType == FieldType.Guid)
                        s.Add($"            {field.Name} = Guid.NewGuid();");
                }
                s.Add($"        }}");
            }

            // tostring override
            if (CurrentEntity.PrimaryFieldId.HasValue)
            {
                s.Add($"");
                s.Add($"        public override string ToString()");
                s.Add($"        {{");
                if (CurrentEntity.PrimaryField.NetType == "string")
                    s.Add($"            return {CurrentEntity.PrimaryField.Name};");
                else
                    s.Add($"            return Convert.ToString({CurrentEntity.PrimaryField.Name});");
                s.Add($"        }}");
            }

            s.Add($"");
            s.Add($"        public override bool Equals(object obj)");
            s.Add($"        {{");
            s.Add($"            if (obj == null || GetType() != obj.GetType()) return false;");
            s.Add($"");
            s.Add($"            {CurrentEntity.Name} other = ({CurrentEntity.Name})obj;");
            s.Add($"");
            s.Add($"            return {string.Join(" && ", CurrentEntity.KeyFields.Select(o => $"{o.Name} == other.{o.Name}"))};");
            s.Add($"        }}");

            s.Add($"");
            s.Add($"        public override int GetHashCode()");
            s.Add($"        {{");
            if (CurrentEntity.KeyFields.Count() == 1)
            {
                s.Add($"            return {CurrentEntity.KeyFields.First().Name}.GetHashCode();");
            }
            else
            {
                s.Add($"            int hash = 17;");
                foreach (var field in CurrentEntity.KeyFields)
                    s.Add($"            hash = hash * 23 + {field.Name}.GetHashCode();");
                s.Add($"            return hash;");
            }
            s.Add($"        }}");

            s.Add($"    }}");

            if (fileContentsField != null && !fileContentsField.UseAzureBlobStorage)
            {
                s.Add($"");
                s.Add($"    public class {CurrentEntity.Name}Content");
                s.Add($"    {{");
                keyCounter = 0;
                foreach (var keyField in CurrentEntity.KeyFields)
                {
                    var attributes = new List<string>();

                    attributes.Add("Key");
                    attributes.Add("Required");
                    attributes.Add($"ForeignKey(\"{CurrentEntity.Name}\")");
                    if (CurrentEntity.KeyFields.Count > 1)
                        attributes.Add($"Column(Order = {keyCounter})");

                    keyCounter++;



                    if (attributes.Count > 0)
                        s.Add($"        [" + string.Join(", ", attributes) + "]");

                    s.Add($"        public {keyField.NetType} {keyField.Name} {{ get; set; }}");
                }

                s.Add($"");
                if (!fileContentsField.IsNullable)
                    s.Add($"        [Required]");
                s.Add($"        public byte[] {fileContentsField.Name} {{ get; set; }}");

                s.Add($"");
                s.Add($"        [Required]");
                s.Add($"        public virtual {CurrentEntity.Name} {CurrentEntity.Name} {{ get; set; }}");

                s.Add($"    }}");
                s.Add($"");
            }

            s.Add($"}}");

            return RunCodeReplacements(s.ToString(), CodeType.Model);
        }
    }
}