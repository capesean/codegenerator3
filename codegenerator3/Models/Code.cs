using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Data.Entity;
using System.Data;
using System.Data.Entity.Infrastructure;
using System.IO;

/*
 notes on error: Possibly unhandled rejection:
 as soon as you work with the .$promise, you have to include another .catch() 
 if you don't, then you get the error. 
     */

namespace WEB.Models
{
    public class Code
    {
        private Entity CurrentEntity { get; set; }
        private List<Entity> _allEntities { get; set; }
        private List<Entity> NormalEntities { get { return AllEntities.Where(e => e.EntityType == EntityType.Normal && !e.Exclude).ToList(); } }
        private List<Entity> AllEntities
        {
            get
            {
                if (_allEntities == null)
                {
                    _allEntities = DbContext.Entities
                        .Include(e => e.Project)
                        .Include(e => e.Fields)
                        .Include(e => e.CodeReplacements)
                        .Include(e => e.RelationshipsAsChild.Select(p => p.RelationshipFields))
                        .Include(e => e.RelationshipsAsChild.Select(p => p.ParentEntity))
                        .Include(e => e.RelationshipsAsParent.Select(p => p.RelationshipFields))
                        .Include(e => e.RelationshipsAsParent.Select(p => p.ChildEntity))
                        .Where(e => e.ProjectId == CurrentEntity.ProjectId && !e.Exclude).OrderBy(e => e.Name).ToList();
                }
                return _allEntities;
            }
        }
        private List<Lookup> _lookups { get; set; }
        private List<Lookup> Lookups
        {
            get
            {
                if (_lookups == null)
                {
                    _lookups = DbContext.Lookups.Include(l => l.LookupOptions).Where(l => l.ProjectId == CurrentEntity.ProjectId).OrderBy(l => l.Name).ToList();
                }
                return _lookups;
            }
        }
        private List<CodeReplacement> _codeReplacements { get; set; }
        private List<CodeReplacement> CodeReplacements
        {
            get
            {
                if (_codeReplacements == null)
                {
                    _codeReplacements = DbContext.CodeReplacements.Include(cr => cr.Entity).Where(cr => cr.Entity.ProjectId == CurrentEntity.ProjectId).OrderBy(cr => cr.SortOrder).ToList();
                }
                return _codeReplacements;
            }
        }
        private List<RelationshipField> _relationshipFields { get; set; }
        private List<RelationshipField> RelationshipFields
        {
            get
            {
                if (_relationshipFields == null)
                {
                    _relationshipFields = DbContext.RelationshipFields.Where(rf => rf.Relationship.ParentEntity.ProjectId == CurrentEntity.ProjectId).ToList();
                }
                return _relationshipFields;
            }
        }
        private ApplicationDbContext DbContext { get; set; }
        private Entity GetEntity(Guid entityId)
        {
            return AllEntities.Single(e => e.EntityId == entityId);
        }
        private Relationship ParentHierarchyRelationship
        {
            get { return CurrentEntity.RelationshipsAsChild.SingleOrDefault(r => r.Hierarchy); }
        }

        public Code(Entity currentEntity, ApplicationDbContext dbContext)
        {
            CurrentEntity = currentEntity;
            DbContext = dbContext;
        }

        public string GenerateModel()
        {
            if (CurrentEntity.EntityType == EntityType.User)
            {
                if (!CurrentEntity.Fields.Any(o => o.Name == "Id")) throw new Exception("An entity of Type: User must have a field named Id");
                if (!CurrentEntity.Fields.Any(o => o.Name == "Id" && o.KeyField)) throw new Exception("An entity of Type: User must have a field named Id that is a Primary Key");
                if (!CurrentEntity.Fields.Any(o => o.Name == "Email")) throw new Exception("An entity of Type: User must have a field named Email");
            }


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
                    s.Add($"        public {field.NetType.ToString()} {field.Name} {{ get; private set; }}");
                }
                else
                {
                    s.Add($"        public {field.NetType.ToString()} {field.Name} {{ get; set; }}");
                }
                s.Add($"");
            }

            // child entities
            foreach (var relationship in CurrentEntity.RelationshipsAsParent.Where(r => !r.ChildEntity.Exclude && !r.ParentEntity.Exclude).OrderBy(o => o.SortOrder))
            {
                if (!relationship.IsOneToOne)
                    s.Add($"        public virtual ICollection<{GetEntity(relationship.ChildEntityId).Name}> {relationship.CollectionName} {{ get; set; }} = new List<{GetEntity(relationship.ChildEntityId).Name}>();");
                else
                    s.Add($"        public virtual {GetEntity(relationship.ChildEntityId).Name} {relationship.CollectionName} {{ get; set; }}");
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

            s.Add($"    }}");
            s.Add($"}}");

            return RunCodeReplacements(s.ToString(), CodeType.Model);
        }

        public string GenerateTypeScriptModel()
        {
            var s = new StringBuilder();
            s.Add($"import {{ SearchOptions, PagingOptions }} from './http.model';");
            foreach (var relationshipParentEntity in CurrentEntity.RelationshipsAsChild.Where(r => !r.ParentEntity.Exclude && r.ParentEntityId != CurrentEntity.EntityId).Select(o => o.ParentEntity).Distinct().OrderBy(o => o.Name))
            {
                s.Add($"import {{ {relationshipParentEntity.Name} }} from './{relationshipParentEntity.Name.ToLower()}.model';");
            }
            if (CurrentEntity.Fields.Any(o => o.FieldType == FieldType.Enum))
            {
                var lookups = CurrentEntity.Fields.Where(o => o.FieldType == FieldType.Enum).Select(o => o.Lookup.PluralName).OrderBy(o => o).Distinct().Aggregate((current, next) => { return current + ", " + next; });
                s.Add($"import {{ {lookups} }} from './enums.model';");
            }
            s.Add($"");

            s.Add($"export class {CurrentEntity.Name} {{");

            // fields
            foreach (var field in CurrentEntity.Fields.Where(o => o.EditPageType != EditPageType.Exclude).OrderBy(f => f.FieldOrder))
            {
                s.Add($"    {field.Name.ToCamelCase()}: {field.JavascriptType};");
            }
            foreach (var relationship in CurrentEntity.RelationshipsAsChild.Where(r => !r.ParentEntity.Exclude).OrderBy(o => o.ParentEntity.Name).ThenBy(o => o.CollectionName))
            {
                if (relationship.RelationshipFields.Count == 1 && relationship.RelationshipFields.Single().ChildField.EditPageType == EditPageType.Exclude) continue;
                s.Add($"    {relationship.ParentName.ToCamelCase()}: {relationship.ParentEntity.Name};");
            }
            if (CurrentEntity.EntityType == EntityType.User)
            {
                s.Add($"    roles: string[] = [];");
            }
            s.Add($"");

            s.Add($"    constructor() {{");
            // can't do this for composite key fields, else they all get set to a value (000-000-000) and 
            // then the angular/typescript validation always passes as the value is not-undefined
            if (CurrentEntity.KeyFields.Count() == 1 && CurrentEntity.KeyFields.First().CustomType == CustomType.Guid)
                s.Add($"        this.{CurrentEntity.KeyFields.First().Name.ToCamelCase()} = \"00000000-0000-0000-0000-000000000000\";");
            foreach (var field in CurrentEntity.Fields.OrderBy(o => o.FieldOrder))
                if (!string.IsNullOrWhiteSpace(field.EditPageDefault))
                    s.Add($"        this.{field.Name.ToCamelCase()} = {field.EditPageDefault};");
            s.Add($"    }}");
            s.Add($"}}");
            s.Add($"");

            s.Add($"export class {CurrentEntity.Name}SearchOptions extends SearchOptions {{");

            if (CurrentEntity.Fields.Any(f => f.SearchType == SearchType.Text))
            {
                s.Add($"    q: string;");// = undefined
            }
            foreach (var field in CurrentEntity.Fields.Where(f => f.SearchType == SearchType.Exact).OrderBy(f => f.FieldOrder))
            {
                s.Add($"    {field.Name.ToCamelCase()}: {field.JavascriptType};");
            }
            foreach (var field in CurrentEntity.Fields.Where(f => f.SearchType == SearchType.Range).OrderBy(f => f.FieldOrder))
            {
                s.Add($"    from{field.Name}: {field.JavascriptType};");
                s.Add($"    to{field.Name}: {field.JavascriptType};");
            }
            if (CurrentEntity.EntityType == EntityType.User)
            {
                s.Add($"    roleName: string;");
            }

            s.Add($"}}");
            s.Add($"");

            s.Add($"export class {CurrentEntity.Name}SearchResponse {{");
            s.Add($"    {CurrentEntity.PluralName.ToCamelCase()}: {CurrentEntity.Name}[] = [];");
            s.Add($"    headers: PagingOptions;");
            s.Add($"}}");

            return RunCodeReplacements(s.ToString(), CodeType.TypeScriptModel);
        }

        public string GenerateTypeScriptEnums()
        {
            var s = new StringBuilder();

            s.Add($"export class Enum {{");
            s.Add($"    value: number;");
            s.Add($"    name: string;");
            s.Add($"    label: string;");
            s.Add($"}}");
            s.Add($"");

            foreach (var lookup in Lookups.Where(o => !o.IsRoleList))
            {
                s.Add($"export enum {lookup.PluralName} {{");
                var options = lookup.LookupOptions.OrderBy(o => o.SortOrder);
                foreach (var option in options)
                    s.Add($"    {option.Name}{(option.Value.HasValue ? " = " + option.Value : string.Empty)}" + (option == options.Last() ? string.Empty : ","));
                s.Add($"}}");
                s.Add($"");
            }

            s.Add($"export class Enums {{");
            s.Add($"");
            foreach (var lookup in Lookups.Where(o => !o.IsRoleList))
            {
                s.Add($"     static {lookup.PluralName}: Enum[] = [");
                var options = lookup.LookupOptions.OrderBy(o => o.SortOrder);
                var counter = 0;
                foreach (var option in options)
                {
                    s.Add($"        {{ value: {(option.Value.HasValue ? option.Value : counter)}, name: '{option.Name}', label: '{option.FriendlyName}' }}" + (option == options.Last() ? string.Empty : ","));
                    counter++;
                }
                s.Add($"     ]");
                s.Add($"");
            }
            s.Add($"}}");

            return RunCodeReplacements(s.ToString(), CodeType.TypeScriptEnums);
        }

        public string GenerateEnums()
        {
            var s = new StringBuilder();

            s.Add($"namespace {CurrentEntity.Project.Namespace}.Models");
            s.Add($"{{");
            foreach (var lookup in Lookups.Where(o => !o.IsRoleList))
            {
                s.Add($"    public enum " + lookup.Name);
                s.Add($"    {{");
                var options = lookup.LookupOptions.OrderBy(o => o.SortOrder);
                foreach (var option in options)
                    s.Add($"        {option.Name}{(option.Value.HasValue ? " = " + option.Value : string.Empty)}" + (option == options.Last() ? string.Empty : ","));
                s.Add($"    }}");
                s.Add($"");
            }
            s.Add($"    public static class Extensions");
            s.Add($"    {{");
            foreach (var lookup in Lookups.Where(o => !o.IsRoleList))
            {
                s.Add($"        public static string Label(this {lookup.Name} {lookup.Name.ToCamelCase()})");
                s.Add($"        {{");
                s.Add($"            switch ({lookup.Name.ToCamelCase()})");
                s.Add($"            {{");
                var options = lookup.LookupOptions.OrderBy(o => o.SortOrder);
                foreach (var option in options)
                {
                    s.Add($"                case {lookup.Name}.{option.Name}:");
                    s.Add($"                    return \"{option.FriendlyName.Replace("\"", "\\\"")}\";");
                }
                s.Add($"                default:");
                s.Add($"                    return null;");
                s.Add($"            }}");
                s.Add($"        }}");
                s.Add($"");
            }
            s.Add($"    }}");
            s.Add($"}}");

            return RunCodeReplacements(s.ToString(), CodeType.Enums);
        }

        public string GenerateTypeScriptRoles()
        {
            var s = new StringBuilder();

            s.Add($"export class Role {{");
            s.Add($"   value: number;");
            s.Add($"   name: string;");
            s.Add($"   label: string;");
            s.Add($"}}");
            s.Add($"");

            s.Add($"export class Roles {{");
            if (CurrentEntity.Project.Lookups.Where(o => o.IsRoleList).Count() > 1) throw new Exception("Project has more than 1 IsRoleList Lookups");
            var roleLookup = CurrentEntity.Project.Lookups.SingleOrDefault(o => o.IsRoleList);
            if (roleLookup == null)
            {
                s.Add($"   static List: Roles[] = [];");
            }
            else
            {
                var counter = 0;
                foreach (var option in roleLookup.LookupOptions.OrderBy(o => o.Name))
                {
                    s.Add($"   static {option.Name}: Role = {{ value: {(option.Value.HasValue ? option.Value : counter)}, name: '{option.Name}', label: '{option.FriendlyName}' }};");
                    counter++;
                }
                s.Add($"   static List: Role[] = [{(roleLookup.LookupOptions.Select(o => "Roles." + o.Name).OrderBy(o => o).Aggregate((current, next) => { return current + ", " + next; }))}];");
            }
            s.Add($"}}");

            return RunCodeReplacements(s.ToString(), CodeType.TypeScriptRoles);
        }

        public string GenerateRoles()
        {
            var s = new StringBuilder();

            if (CurrentEntity.Project.Lookups.Where(o => o.IsRoleList).Count() > 1) throw new Exception("Project has more than 1 IsRoleList Lookups");
            var roleLookup = CurrentEntity.Project.Lookups.SingleOrDefault(o => o.IsRoleList);

            s.Add($"namespace WEB.Models");
            s.Add($"{{");
            s.Add($"    public enum Roles");
            s.Add($"    {{");
            if (roleLookup != null)
                s.Add($"        " + roleLookup.LookupOptions.Select(o => o.Name).OrderBy(o => o).Aggregate((current, next) => { return current + ", " + next; }));
            s.Add($"    }}");
            s.Add($"}}");

            return RunCodeReplacements(s.ToString(), CodeType.Roles);
        }

        //public string GenerateSettingsDTO()
        //{
        //    var s = new StringBuilder();

        //    s.Add($"using System;");
        //    s.Add($"using System.Collections.Generic;");
        //    s.Add($"");
        //    s.Add($"namespace {CurrentEntity.Project.Namespace}.Models");
        //    s.Add($"{{");
        //    s.Add($"    public partial class SettingsDTO");
        //    s.Add($"    {{");
        //    //foreach (var lookup in Lookups.Where(o => !o.IsRoleList))
        //    //    s.Add($"        public List<EnumDTO> {lookup.Name} {{ get; set; }}");
        //    s.Add($"");
        //    s.Add($"        public SettingsDTO()");
        //    s.Add($"        {{");
        //    //foreach (var lookup in Lookups.Where(o => !o.IsRoleList))
        //    //{
        //    //    s.Add($"            {lookup.Name} = new List<EnumDTO>();");
        //    //    s.Add($"            foreach ({lookup.Name} type in Enum.GetValues(typeof({lookup.Name})))");
        //    //    s.Add($"                {lookup.Name}.Add(new EnumDTO {{ Id = (int)type, Name = type.ToString(), Label = type.Label() }});");
        //    //    s.Add($"");
        //    //}
        //    s.Add($"        }}");
        //    s.Add($"    }}");
        //    s.Add($"}}");

        //    return RunCodeReplacements(s.ToString(), CodeType.SettingsDTO);
        //}

        public string GenerateDTO()
        {
            //if (CurrentEntity.EntityType == EntityType.User) return string.Empty;

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
                    if (field.NetType == "string" && field.Length > 0)
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
            if (CurrentEntity.EntityType == EntityType.User)
            {
                s.Add($"        public IList<string> Roles {{ get; set; }}");
                s.Add($"");
            }
            s.Add($"    }}");
            s.Add($"");
            s.Add($"    public static partial class ModelFactory");
            s.Add($"    {{");
            s.Add($"        public static {CurrentEntity.DTOName} Create({CurrentEntity.Name} {CurrentEntity.CamelCaseName}{(CurrentEntity.EntityType == EntityType.User ? ", List<AppRole> appRoles = null" : "")})");
            s.Add($"        {{");
            s.Add($"            if ({CurrentEntity.CamelCaseName} == null) return null;");
            s.Add($"");
            if (CurrentEntity.EntityType == EntityType.User)
            {
                s.Add($"            var roles = new List<string>();");
                s.Add($"            if ({CurrentEntity.CamelCaseName}.Roles != null && appRoles != null)");
                s.Add($"                roles = appRoles.Where(o => user.Roles.Any(r => r.RoleId == o.Id)).Select(o => o.Name).ToList();");
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
                s.Add($"            {CurrentEntity.DTOName.ToCamelCase()}.Roles = roles;");
            }
            foreach (var relationship in CurrentEntity.RelationshipsAsChild.OrderBy(o => o.ParentFriendlyName))
            {
                // using exclude to avoid circular references. example: KTU-PACK: version => localisation => contentset => version (UpdateFromVersion)
                if (relationship.RelationshipAncestorLimit == RelationshipAncestorLimits.Exclude) continue;
                if (relationship.RelationshipFields.Count == 1 && relationship.RelationshipFields.First().ChildField.EditPageType == EditPageType.Exclude) continue;
                s.Add($"            {CurrentEntity.DTOName.ToCamelCase()}.{relationship.ParentName} = Create({CurrentEntity.CamelCaseName}.{relationship.ParentName});");
            }
            s.Add($"");
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

        public string GenerateDbContext()
        {
            var calculatedFields = AllEntities.SelectMany(e => e.Fields).Where(f => f.EditPageType == EditPageType.CalculatedField);

            var s = new StringBuilder();

            s.Add($"using Microsoft.EntityFrameworkCore;");
            s.Add($"");
            s.Add($"namespace {CurrentEntity.Project.Namespace}.Models");
            s.Add($"{{");
            s.Add($"    public partial class ApplicationDbContext");
            s.Add($"    {{");
            foreach (var e in NormalEntities)
                s.Add($"        public DbSet<{e.Name}> {e.PluralName} {{ get; set; }}");
            s.Add($"");
            s.Add($"        public void ConfigureModelBuilder(ModelBuilder modelBuilder)");
            s.Add($"        {{");
            //foreach (var field in calculatedFields)
            //{
            //    s.Add($"            modelBuilder.Entity<{field.Entity.Name}>().Ignore(t => t.{field.Name});");
            //}
            //if (calculatedFields.Count() > 0) s.Add($"");

            foreach (var relationship in DbContext.Relationships
                .Include(o => o.ParentEntity)
                .Include(o => o.ChildEntity)
                .Include(o => o.RelationshipFields.Select(p => p.ChildField))
               .Where(o => o.IsOneToOne && o.ParentEntity.ProjectId == CurrentEntity.ProjectId))
            {
                s.Add($"            modelBuilder.Entity<{relationship.ParentEntity.Name}>()");
                s.Add($"                .HasOne(o => o.{relationship.ChildEntity.Name})");
                s.Add($"                .WithOne(o => o.{relationship.ParentEntity.Name})");
                s.Add($"                .HasForeignKey<{relationship.ChildEntity.Name}>(o => o.{relationship.RelationshipFields.First().ChildField.Name});");
                s.Add($"");
            }


            foreach (var entity in AllEntities.OrderBy(o => o.Name))
            {
                var needsBreak = false;
                if (entity.KeyFields.Count > 1)
                {
                    s.Add($"            modelBuilder.Entity<{entity.Name}>()");
                    s.Add($"                .HasKey(o => new {{ {entity.KeyFields.Select(o => "o." + o.Name).Aggregate((current, next) => current + ", " + next)} }})");
                    s.Add($"                .HasName(\"PK_{entity.Name}\");");
                    needsBreak = true;
                }
                foreach (var field in entity.Fields.Where(o => !o.IsNullable && o.IsUnique).OrderBy(f => f.FieldOrder))
                {
                    if (field.IsUniqueOnHierarchy)
                    {
                        var rel = entity.RelationshipsAsChild.SingleOrDefault(r => r.Hierarchy);
                        if (rel == null) throw new Exception($"Field has IsUniqueOnHierarchy but no hierarchy: {entity.Name}.{field.Name}");
                        s.Add($"            modelBuilder.Entity<{entity.Name}>()");
                        s.Add($"                .HasIndex(o => new {{ o.{rel.RelationshipFields.First().ChildField.Name}, o.{field.Name} }})");
                        s.Add($"                .HasDatabaseName(\"IX_{entity.Name}_{field.Name}\")");
                        s.Add($"                .IsUnique();");
                        needsBreak = true;
                    }
                    else if (field.IsUnique)
                    {
                        s.Add($"            modelBuilder.Entity<{entity.Name}>()");
                        s.Add($"                .HasIndex(o => o.{field.Name})");
                        s.Add($"                .HasDatabaseName(\"IX_{entity.Name}_{field.Name}\")");
                        s.Add($"                .IsUnique();");
                        needsBreak = true;
                    }
                }
                if (needsBreak) s.Add($"");
            }

            foreach (var entity in AllEntities.OrderBy(o => o.Name))
            {
                foreach (var relationship in entity.RelationshipsAsChild.OrderBy(o => o.SortOrder).ThenBy(o => o.ParentEntity.Name))
                {
                    if (!entity.RelationshipsAsChild.Any(r => r.ParentEntityId == relationship.ParentEntityId && r.RelationshipId != relationship.RelationshipId)) continue;
                    s.Add($"            modelBuilder.Entity<{entity.Name}>()");
                    s.Add($"                .HasOne(o => o.{relationship.ParentName})");
                    s.Add($"                .WithMany(o => o.{relationship.CollectionName})");
                    if (!relationship.RelationshipFields.First().ChildField.IsNullable)
                        s.Add($"                .IsRequired()");
                    s.Add($"                .HasForeignKey(o => o.{relationship.RelationshipFields.First().ChildField.Name});");
                    s.Add($"");
                }
            }

            var smallDateTimeFields = DbContext.Fields.Where(f => f.FieldType == FieldType.SmallDateTime && f.Entity.ProjectId == CurrentEntity.ProjectId).OrderBy(f => f.Entity.Name).ThenBy(f => f.FieldOrder).ToList();
            foreach (var field in smallDateTimeFields.OrderBy(o => o.Entity.Name).ThenBy(o => o.FieldOrder))
            {
                if (field.EditPageType == EditPageType.CalculatedField) continue;
                s.Add($"            modelBuilder.Entity<{field.Entity.Name}>().Property(o => o.{field.Name}).HasColumnType(\"smalldatetime\");");
            }
            s.Add($"        }}");

            if (calculatedFields.Count() > 0)
            {
                s.Add($"");
                s.Add($"        public void AddComputedColumns()");
                s.Add($"        {{");
                foreach (var field in calculatedFields)
                {
                    s.Add($"            CreateComputedColumn(\"{(field.Entity.EntityType == EntityType.User ? "AspNetUsers" : field.Entity.PluralName)}\", \"{field.Name}\", \"{field.CalculatedFieldDefinition.Replace("\"", "'")}\");");
                }
                s.Add($"        }}");
            }
            var nullableUniques = DbContext.Fields.Where(o => o.IsUnique && o.IsNullable && o.Entity.ProjectId == CurrentEntity.ProjectId).ToList();
            s.Add($"");
            s.Add($"        public void AddNullableUniqueIndexes()");
            s.Add($"        {{");
            foreach (var field in nullableUniques.OrderBy(o => o.Entity.Name).ThenBy(o => o.Name))
            {
                s.Add($"            CreateNullableUniqueIndex(\"{field.Entity.PluralName}\", \"{field.Name}\");");
            }
            s.Add($"        }}");
            s.Add($"    }}");
            s.Add($"}}");

            return RunCodeReplacements(s.ToString(), CodeType.DbContext);
        }

        public string GenerateController()
        {
            //if (CurrentEntity.EntityType == EntityType.User) return string.Empty;

            var s = new StringBuilder();

            s.Add($"using System;");
            s.Add($"using System.Linq;");
            s.Add($"using System.Threading.Tasks;");
            s.Add($"using Microsoft.AspNetCore.Mvc;");
            s.Add($"using Microsoft.AspNetCore.Identity;");
            s.Add($"using Microsoft.AspNetCore.Authorization;");
            s.Add($"using Microsoft.EntityFrameworkCore;");
            s.Add($"using {CurrentEntity.Project.Namespace}.Models;");
            if (CurrentEntity.EntityType == EntityType.User)
                s.Add($"using Microsoft.Extensions.Options;");
            s.Add($"");
            s.Add($"namespace {CurrentEntity.Project.Namespace}.Controllers");
            s.Add($"{{");
            s.Add($"    [Route(\"api/[Controller]\"), Authorize]");
            s.Add($"    public {(CurrentEntity.PartialControllerClass ? "partial " : string.Empty)}class {CurrentEntity.PluralName}Controller : BaseApiController");
            s.Add($"    {{");
            if (CurrentEntity.EntityType == EntityType.User)
            {
                s.Add($"        private RoleManager<AppRole> rm;");
                s.Add($"        private IOptions<PasswordOptions> opts;");
                s.Add($"");
                s.Add($"        public UsersController(ApplicationDbContext db, UserManager<User> um, Settings settings, RoleManager<AppRole> rm, IOptions<PasswordOptions> opts)");
                s.Add($"            : base(db, um, settings) {{ this.rm = rm; this.opts = opts; }}");
            }
            else
            {
                s.Add($"        public {CurrentEntity.PluralName}Controller(ApplicationDbContext db, UserManager<User> um, Settings settings) : base(db, um, settings) {{ }}");
            }
            s.Add($"");

            #region search
            s.Add($"        [HttpGet{(CurrentEntity.AuthorizationType == AuthorizationType.ProtectAll ? ", AuthorizeRoles(Roles.Administrator)" : string.Empty)}]");

            var fieldsToSearch = new List<Field>();
            var roleSearch = string.Empty;
            if (CurrentEntity.EntityType == EntityType.User)
                roleSearch = ", string roleName = null";

            foreach (var relationship in CurrentEntity.RelationshipsAsChild.OrderBy(r => r.RelationshipFields.Min(f => f.ChildField.FieldOrder)))
                foreach (var relationshipField in relationship.RelationshipFields)
                    fieldsToSearch.Add(relationshipField.ChildField);
            foreach (var field in CurrentEntity.ExactSearchFields)
                if (!fieldsToSearch.Contains(field))
                    fieldsToSearch.Add(field);
            foreach (var field in CurrentEntity.RangeSearchFields)
                fieldsToSearch.Add(field);

            s.Add($"        public async Task<IActionResult> Search([FromQuery] PagingOptions pagingOptions{(CurrentEntity.TextSearchFields.Count > 0 ? ", [FromQuery] string q = null" : "")}{roleSearch}{(fieldsToSearch.Count > 0 ? $", {fieldsToSearch.Select(f => f.ControllerSearchParams).Aggregate((current, next) => current + ", " + next)}" : "")})");
            s.Add($"        {{");
            s.Add($"            if (pagingOptions == null) pagingOptions = new PagingOptions();");
            s.Add($"");

            if (CurrentEntity.EntityType == EntityType.User)
            {
                s.Add($"            IQueryable<User> results = userManager.Users;");
                if (CurrentEntity.HasUserFilterField)
                    s.Add($"            results = results.Where(o => o.{CurrentEntity.UserFilterFieldPath} == CurrentUser.{CurrentEntity.Project.UserFilterFieldName});");
                s.Add($"            results = results.Include(o => o.Roles);");
                s.Add($"");
                // todo: fix...
                //s.Add($"            if (roleName != null) results = results.Where(o => o.Roles.Any(r => r.Role.Name == roleName));");
                s.Add($"");
            }
            else
            {
                s.Add($"            IQueryable<{CurrentEntity.Name}> results = {CurrentEntity.Project.DbContextVariable}.{CurrentEntity.PluralName}{(CurrentEntity.HasUserFilterField ? "" : ";")}");
                if (CurrentEntity.HasUserFilterField)
                    s.Add($"                .Where(o => o.{CurrentEntity.UserFilterFieldPath} == CurrentUser.{CurrentEntity.Project.UserFilterFieldName});");
                s.Add($"");
            }

            if (CurrentEntity.RelationshipsAsChild.Where(r => r.RelationshipAncestorLimit != RelationshipAncestorLimits.Exclude).Any())
            {
                s.Add($"            if (pagingOptions.IncludeEntities)");
                s.Add($"            {{");
                foreach (var relationship in CurrentEntity.RelationshipsAsChild.OrderBy(r => r.SortOrderOnChild).ThenBy(o => o.ParentName))
                {
                    if (relationship.RelationshipAncestorLimit == RelationshipAncestorLimits.Exclude) continue;

                    foreach (var result in GetTopAncestors(new List<string>(), "o", relationship, relationship.RelationshipAncestorLimit, includeIfHierarchy: true))
                        s.Add($"                results = results.Include(o => {result});");
                }
                s.Add($"            }}");
                s.Add($"");
            }

            if (CurrentEntity.TextSearchFields.Count > 0)
            {
                s.Add($"            if (!string.IsNullOrWhiteSpace(q))");
                s.Add($"                results = results.Where(o => {(CurrentEntity.EntityType == EntityType.User ? "o.Email.Contains(q) || " : "")}{CurrentEntity.TextSearchFields.Select(o => $"o.{o.Name + (o.CustomType == CustomType.String ? string.Empty : ".ToString()")}.Contains(q)").Aggregate((current, next) => current + " || " + next)});");
                s.Add($"");
            }

            if (fieldsToSearch.Count > 0)
            {
                foreach (var field in fieldsToSearch)
                {
                    if (field.SearchType == SearchType.Range && field.CustomType == CustomType.Date)
                    {
                        //s.Add($"            if (from{field.Name}.HasValue) {{ var from{field.Name}Utc = from{field.Name}.Value.ToUniversalTime(); results = results.Where(o => o.{ field.Name} >= from{field.Name}Utc); }}");
                        //s.Add($"            if (to{field.Name}.HasValue) {{ var to{field.Name}Utc = to{field.Name}.Value.ToUniversalTime(); results = results.Where(o => o.{ field.Name} <= to{field.Name}Utc); }}");
                        // disabled: in covid.distribution, the date is sent as 2020-04-18, so no conversion needed, else it chops off the end date
                        s.Add($"            if (from{field.Name}.HasValue) results = results.Where(o => o.{field.Name} >= from{field.Name}.Value.Date);");
                        s.Add($"            if (to{field.Name}.HasValue) results = results.Where(o => o.{field.Name} < to{field.Name}.Value.Date.AddDays(1));");
                    }
                    else
                    {
                        s.Add($"            if ({field.Name.ToCamelCase()}{(field.CustomType == CustomType.String ? " != null" : ".HasValue")}) results = results.Where(o => o.{field.Name} == {field.Name.ToCamelCase()});");
                    }
                }
                s.Add($"");
            }

            if (CurrentEntity.SortOrderFields.Count > 0)
                s.Add($"            results = results.Order{CurrentEntity.SortOrderFields.Select(f => "By" + (f.SortDescending ? "Descending" : string.Empty) + "(o => o." + f.Name + ")").Aggregate((current, next) => current + ".Then" + next)};");

            //var counter = 0;
            //foreach (var field in CurrentEntity.SortOrderFields)
            //{
            //    s.Add($"            results = results.{(counter == 0 ? "Order" : "Then")}By(o => o.{field.Name});");
            //    counter++;
            //}
            s.Add($"");
            if (CurrentEntity.EntityType == EntityType.User)
            {
                s.Add($"            var roles = await db.Roles.ToListAsync();");
                s.Add($"");
                s.Add($"            return Ok((await GetPaginatedResponse(results, pagingOptions)).Select(o => ModelFactory.Create(o, roles)));");
            }
            else
            {
                s.Add($"            return Ok((await GetPaginatedResponse(results, pagingOptions)).Select(o => ModelFactory.Create(o)));");
            }
            s.Add($"        }}");
            s.Add($"");
            #endregion

            #region get
            s.Add($"        [HttpGet(\"{CurrentEntity.RoutePath}\"){(CurrentEntity.AuthorizationType == AuthorizationType.ProtectAll ? ", AuthorizeRoles(Roles.Administrator)" : string.Empty)}]");
            s.Add($"        public async Task<IActionResult> Get({CurrentEntity.ControllerParameters})");
            s.Add($"        {{");
            if (CurrentEntity.EntityType == EntityType.User)
            {
                s.Add($"            var user = await userManager.Users");
                s.Add($"                .Include(o => o.Roles)");
            }
            else
            {
                s.Add($"            var {CurrentEntity.CamelCaseName} = await {CurrentEntity.Project.DbContextVariable}.{CurrentEntity.PluralName}");
            }
            foreach (var relationship in CurrentEntity.RelationshipsAsChild.OrderBy(r => r.SortOrder).ThenBy(o => o.ParentName))
            {
                if (relationship.RelationshipAncestorLimit == RelationshipAncestorLimits.Exclude) continue;

                //if (relationship.Hierarchy) -- these are needed e.g. when using select-directives, then it needs to have the related entities to show the label (not just the id, which nya-bs-select could make do with)
                foreach (var result in GetTopAncestors(new List<string>(), "o", relationship, relationship.RelationshipAncestorLimit, includeIfHierarchy: true))
                    s.Add($"                .Include(o => {result})");
            }
            if (CurrentEntity.HasUserFilterField)
                s.Add($"                .Where(o => o.{CurrentEntity.UserFilterFieldPath} == CurrentUser.{CurrentEntity.Project.UserFilterFieldName})");
            //s.Add($"                .AsNoTracking()");
            s.Add($"                .FirstOrDefaultAsync(o => {GetKeyFieldLinq("o")});");
            s.Add($"");
            s.Add($"            if ({CurrentEntity.CamelCaseName} == null)");
            s.Add($"                return NotFound();");
            s.Add($"");
            if (CurrentEntity.EntityType == EntityType.User)
            {
                s.Add($"            var roles = await db.Roles.ToListAsync();");
                s.Add($"");
                s.Add($"            return Ok(ModelFactory.Create({CurrentEntity.CamelCaseName}, roles));");
            }
            else
            {
                s.Add($"            return Ok(ModelFactory.Create({CurrentEntity.CamelCaseName}));");
            }
            s.Add($"        }}");
            s.Add($"");
            #endregion

            #region save
            s.Add($"        [HttpPost(\"{CurrentEntity.RoutePath}\"){(CurrentEntity.AuthorizationType == AuthorizationType.None ? "" : ", AuthorizeRoles(Roles.Administrator)")}]");
            s.Add($"        public async Task<IActionResult> Save({CurrentEntity.ControllerParameters}, [FromBody] {CurrentEntity.DTOName} {CurrentEntity.DTOName.ToCamelCase()})");
            s.Add($"        {{");
            s.Add($"            if (!ModelState.IsValid) return BadRequest(ModelState);");
            s.Add($"");
            s.Add($"            if ({GetKeyFieldLinq(CurrentEntity.DTOName.ToCamelCase(), null, "!=", "||")}) return BadRequest(\"Id mismatch\");");
            s.Add($"");
            foreach (var field in CurrentEntity.Fields.Where(f => f.IsUnique && f.EditPageType != EditPageType.Exclude).OrderBy(f => f.FieldOrder))
            {
                if (field.EditPageType == EditPageType.ReadOnly) continue;

                string hierarchyFields = string.Empty;
                if (field.IsUniqueOnHierarchy)
                {
                    foreach (var relField in ParentHierarchyRelationship.RelationshipFields)
                        hierarchyFields += (hierarchyFields == string.Empty ? "" : " && ") + "o." + relField.ChildField.Name + " == " + CurrentEntity.DTOName.ToCamelCase() + "." + relField.ChildField.Name;
                    hierarchyFields += " && ";
                }
                s.Add($"            if (" + (field.IsNullable ? field.NotNullCheck(CurrentEntity.DTOName.ToCamelCase() + "." + field.Name) + " && " : "") + $"await {CurrentEntity.Project.DbContextVariable}.{CurrentEntity.PluralName}.AnyAsync(o => {hierarchyFields}o.{field.Name} == {CurrentEntity.DTOName.ToCamelCase()}.{field.Name} && {GetKeyFieldLinq("o", CurrentEntity.DTOName.ToCamelCase(), "!=", "||", true)}))");
                s.Add($"                return BadRequest(\"{field.Label} already exists{(field.IsUniqueOnHierarchy ? " on this " + ParentHierarchyRelationship.ParentEntity.FriendlyName : string.Empty)}.\");");
                s.Add($"");
            }
            if (CurrentEntity.HasCompositePrimaryKey)
            {
                // composite keys don't use the insert method, they use the update for both inserts & updates
                s.Add($"            var {CurrentEntity.CamelCaseName} = await {CurrentEntity.Project.DbContextVariable}.{CurrentEntity.PluralName}");
                if (CurrentEntity.HasUserFilterField)
                    s.Add($"                .Where(o => o.{CurrentEntity.UserFilterFieldPath} == CurrentUser.{CurrentEntity.Project.UserFilterFieldName})");
                s.Add($"                .FirstOrDefaultAsync(o => {GetKeyFieldLinq("o", CurrentEntity.DTOName.ToCamelCase())});");
                s.Add($"            var isNew = {CurrentEntity.CamelCaseName} == null;");
                s.Add($"");
                s.Add($"            if (isNew)");
                s.Add($"            {{");
                s.Add($"                {CurrentEntity.CamelCaseName} = new {CurrentEntity.Name}();");
                s.Add($"");
                foreach (var field in CurrentEntity.Fields.Where(f => f.KeyField && f.Entity.RelationshipsAsChild.Any(r => r.RelationshipFields.Any(rf => rf.ChildFieldId == f.FieldId))).OrderBy(f => f.FieldOrder))
                {
                    s.Add($"                {CurrentEntity.CamelCaseName}.{field.Name} = {CurrentEntity.DTOName.ToCamelCase() + "." + field.Name};");
                }
                foreach (var field in CurrentEntity.Fields.Where(f => !string.IsNullOrWhiteSpace(f.ControllerInsertOverride)).OrderBy(f => f.FieldOrder))
                {
                    s.Add($"                {CurrentEntity.CamelCaseName}.{field.Name} = {field.ControllerInsertOverride};");
                }
                if (CurrentEntity.Fields.Any(f => !string.IsNullOrWhiteSpace(f.ControllerInsertOverride)) || CurrentEntity.Fields.Any(f => f.KeyField && f.Entity.RelationshipsAsChild.Any(r => r.RelationshipFields.Any(rf => rf.ChildFieldId == f.FieldId))))
                    s.Add($"");
                if (CurrentEntity.HasASortField)
                {
                    var field = CurrentEntity.SortField;
                    var sort = $"                {CurrentEntity.DTOName.ToCamelCase()}.{field.Name} = (await {CurrentEntity.Project.DbContextVariable}.{CurrentEntity.PluralName}";
                    if (CurrentEntity.RelationshipsAsChild.Any(r => r.Hierarchy) && CurrentEntity.RelationshipsAsChild.Count(r => r.Hierarchy) == 1)
                    {
                        sort += ".Where(o => " + (CurrentEntity.RelationshipsAsChild.Single(r => r.Hierarchy).RelationshipFields.Select(o => $"o.{o.ChildField.Name} == {CurrentEntity.DTOName.ToCamelCase()}.{o.ChildField.Name}").Aggregate((current, next) => current + " && " + next)) + ")";
                    }
                    sort += $".MaxAsync(o => (int?)o.{field.Name}) ?? -1) + 1;";
                    s.Add(sort);
                    s.Add($"");
                }
                s.Add($"                {CurrentEntity.Project.DbContextVariable}.Entry({CurrentEntity.CamelCaseName}).State = EntityState.Added;");
                s.Add($"            }}");
                s.Add($"            else");
                s.Add($"            {{");
                foreach (var field in CurrentEntity.Fields.Where(f => !string.IsNullOrWhiteSpace(f.ControllerUpdateOverride)).OrderBy(f => f.FieldOrder))
                {
                    s.Add($"                {CurrentEntity.CamelCaseName}.{field.Name} = {field.ControllerUpdateOverride};");
                }
                if (CurrentEntity.Fields.Any(f => !string.IsNullOrWhiteSpace(f.ControllerUpdateOverride)))
                    s.Add($"");
                s.Add($"                {CurrentEntity.Project.DbContextVariable}.Entry({CurrentEntity.CamelCaseName}).State = EntityState.Modified;");
                s.Add($"            }}");
            }
            else
            {
                if (CurrentEntity.EntityType == EntityType.User)
                {
                    s.Add($"            var password = string.Empty;");
                    s.Add($"            if (await db.Users.AnyAsync(o => o.Email == userDTO.Email && o.Id != userDTO.Id))");
                    s.Add($"                return BadRequest(\"Email already exists.\");");
                    s.Add($"");
                }
                s.Add($"            var isNew = {CurrentEntity.KeyFields.Select(f => CurrentEntity.DTOName.ToCamelCase() + "." + f.Name + " == " + f.EmptyValue).Aggregate((current, next) => current + " && " + next)};");
                s.Add($"");
                s.Add($"            {CurrentEntity.Name} {CurrentEntity.CamelCaseName};");
                s.Add($"            if (isNew)");
                s.Add($"            {{");
                s.Add($"                {CurrentEntity.CamelCaseName} = new {CurrentEntity.Name}();");
                if (CurrentEntity.EntityType == EntityType.User)
                    s.Add($"                password = Utilities.General.GenerateRandomPassword(opts.Value);");
                s.Add($"");
                foreach (var field in CurrentEntity.Fields.Where(f => !string.IsNullOrWhiteSpace(f.ControllerInsertOverride)).OrderBy(f => f.FieldOrder))
                {
                    s.Add($"                {CurrentEntity.CamelCaseName}.{field.Name} = {field.ControllerInsertOverride};");
                }
                if (CurrentEntity.Fields.Any(f => !string.IsNullOrWhiteSpace(f.ControllerInsertOverride)))
                    s.Add($"");
                if (CurrentEntity.HasASortField)
                {
                    var field = CurrentEntity.SortField;
                    var sort = $"                {CurrentEntity.DTOName.ToCamelCase()}.{field.Name} = (await {CurrentEntity.Project.DbContextVariable}.{CurrentEntity.PluralName}";
                    if (CurrentEntity.RelationshipsAsChild.Any(r => r.Hierarchy) && CurrentEntity.RelationshipsAsChild.Count(r => r.Hierarchy) == 1)
                    {
                        sort += ".Where(o => " + (CurrentEntity.RelationshipsAsChild.Single(r => r.Hierarchy).RelationshipFields.Select(o => $"o.{o.ChildField.Name} == {CurrentEntity.DTOName.ToCamelCase()}.{o.ChildField.Name}").Aggregate((current, next) => current + " && " + next)) + ")";
                    }
                    sort += $".MaxAsync(o => (int?)o.{field.Name}) ?? 0) + 1;";
                    s.Add(sort);
                    s.Add($"");
                }
                if (CurrentEntity.EntityType != EntityType.User)
                    s.Add($"                {CurrentEntity.Project.DbContextVariable}.Entry({CurrentEntity.CamelCaseName}).State = EntityState.Added;");
                s.Add($"            }}");
                s.Add($"            else");
                s.Add($"            {{");
                if (CurrentEntity.EntityType == EntityType.User)
                {
                    s.Add($"                user = await userManager.Users");
                    if (CurrentEntity.HasUserFilterField)
                        s.Add($"                    .Where(o => o.{CurrentEntity.UserFilterFieldPath} == CurrentUser.{CurrentEntity.Project.UserFilterFieldName})");
                    s.Add($"                    .Include(o => o.Roles)");
                    s.Add($"                    .FirstOrDefaultAsync(o => o.Id == userDTO.Id);");
                }
                else
                {
                    s.Add($"                {CurrentEntity.CamelCaseName} = await {CurrentEntity.Project.DbContextVariable}.{CurrentEntity.PluralName}");
                    if (CurrentEntity.HasUserFilterField)
                        s.Add($"                    .Where(o => o.{CurrentEntity.UserFilterFieldPath} == CurrentUser.{CurrentEntity.Project.UserFilterFieldName})");
                    s.Add($"                    .FirstOrDefaultAsync(o => {GetKeyFieldLinq("o", CurrentEntity.DTOName.ToCamelCase())});");
                }
                s.Add($"");
                s.Add($"                if ({CurrentEntity.CamelCaseName} == null)");
                s.Add($"                    return NotFound();");
                s.Add($"");
                foreach (var field in CurrentEntity.Fields.Where(f => !string.IsNullOrWhiteSpace(f.ControllerUpdateOverride)).OrderBy(f => f.FieldOrder))
                {
                    s.Add($"                {CurrentEntity.CamelCaseName}.{field.Name} = {field.ControllerUpdateOverride};");
                }
                if (CurrentEntity.Fields.Any(f => !string.IsNullOrWhiteSpace(f.ControllerUpdateOverride)))
                    s.Add($"");
                if (CurrentEntity.EntityType != EntityType.User)
                    s.Add($"                {CurrentEntity.Project.DbContextVariable}.Entry({CurrentEntity.CamelCaseName}).State = EntityState.Modified;");
                s.Add($"            }}");
            }
            s.Add($"");
            s.Add($"            ModelFactory.Hydrate({CurrentEntity.CamelCaseName}, {CurrentEntity.DTOName.ToCamelCase()});");
            s.Add($"");
            if (CurrentEntity.EntityType == EntityType.User)
            {
                s.Add($"            var saveResult = (isNew ? await userManager.CreateAsync(user, password) : await userManager.UpdateAsync(user));");
                s.Add($"");
                s.Add($"            if (!saveResult.Succeeded)");
                s.Add($"                return GetErrorResult(saveResult);");
                s.Add($"");
                s.Add($"            var appRoles = await rm.Roles.ToListAsync();");
                s.Add($"");
                s.Add($"            if (!isNew)");
                s.Add($"            {{");
                s.Add($"                foreach (var roleId in user.Roles.ToList())");
                s.Add($"                {{");
                s.Add($"                    var role = rm.Roles.Single(o => o.Id == roleId.RoleId);");
                s.Add($"                    await userManager.RemoveFromRoleAsync(user, role.Name);");
                s.Add($"                }}");
                s.Add($"            }}");
                s.Add($"");
                s.Add($"            if (userDTO.Roles != null)");
                s.Add($"            {{");
                s.Add($"                foreach (var roleName in userDTO.Roles)");
                s.Add($"                {{");
                s.Add($"                    await userManager.AddToRoleAsync(user, roleName);");
                s.Add($"                }}");
                s.Add($"            }}");
                s.Add($"");
                s.Add($"            if (isNew) await Utilities.General.SendWelcomeMailAsync(user, password, Settings);");
            }
            else
            {
                s.Add($"            await {CurrentEntity.Project.DbContextVariable}.SaveChangesAsync();");
            }
            s.Add($"");
            s.Add($"            return await Get({CurrentEntity.KeyFields.Select(f => CurrentEntity.CamelCaseName + "." + f.Name).Aggregate((current, next) => current + ", " + next)});");
            s.Add($"        }}");
            s.Add($"");
            #endregion

            #region delete
            s.Add($"        [HttpDelete(\"{CurrentEntity.RoutePath}\"){(CurrentEntity.AuthorizationType == AuthorizationType.None ? "" : ", AuthorizeRoles(Roles.Administrator)")}]");
            s.Add($"        public async Task<IActionResult> Delete({CurrentEntity.ControllerParameters})");
            s.Add($"        {{");
            s.Add($"            var {CurrentEntity.CamelCaseName} = await {(CurrentEntity.EntityType == EntityType.User ? "userManager" : CurrentEntity.Project.DbContextVariable)}.{CurrentEntity.PluralName}");
            if (CurrentEntity.HasUserFilterField)
                s.Add($"                .Where(o => o.{CurrentEntity.UserFilterFieldPath} == CurrentUser.{CurrentEntity.Project.UserFilterFieldName})");
            s.Add($"                .FirstOrDefaultAsync(o => {GetKeyFieldLinq("o")});");
            s.Add($"");
            s.Add($"            if ({CurrentEntity.CamelCaseName} == null)");
            s.Add($"                return NotFound();");
            s.Add($"");
            foreach (var relationship in CurrentEntity.RelationshipsAsParent.Where(rel => !rel.ChildEntity.Exclude).OrderBy(o => o.SortOrder))
            {
                if (relationship.CascadeDelete)
                {
                    var childEntityName = relationship.ChildEntity.CamelCaseName + (relationship.ChildEntity.EntityId == CurrentEntity.EntityId ? "Child" : "");
                    s.Add($"            foreach (var {childEntityName} in {CurrentEntity.Project.DbContextVariable}.{relationship.ChildEntity.PluralName}.Where(o => {relationship.RelationshipFields.Select(rf => "o." + rf.ChildField.Name + " == " + CurrentEntity.CamelCaseName + "." + rf.ParentField.Name).Aggregate((current, next) => current + " && " + next)}))");
                    s.Add($"                {CurrentEntity.Project.DbContextVariable}.Entry({childEntityName}).State = EntityState.Deleted;");
                    s.Add($"");
                }
                else
                {
                    var joins = relationship.RelationshipFields.Select(o => $"o.{o.ChildField.Name} == {CurrentEntity.CamelCaseName}.{o.ParentField.Name}").Aggregate((current, next) => current + " && " + next);
                    s.Add($"            if (await {CurrentEntity.Project.DbContextVariable}.{(relationship.ChildEntity.EntityType == EntityType.User ? "Users" : relationship.ChildEntity.PluralName)}.AnyAsync(o => {joins}))");
                    s.Add($"                return BadRequest(\"Unable to delete the {CurrentEntity.FriendlyName.ToLower()} as it has related {relationship.ChildEntity.PluralFriendlyName.ToLower()}\");");
                    s.Add($"");
                }
            }

            if (CurrentEntity.EntityType == EntityType.User)
            {
                s.Add($"            foreach (var role in await userManager.GetRolesAsync(user))");
                s.Add($"                await userManager.RemoveFromRoleAsync(user, role);");
                s.Add($"");
                s.Add($"            await userManager.DeleteAsync(user);");
            }
            else
            {
                s.Add($"            {CurrentEntity.Project.DbContextVariable}.Entry({CurrentEntity.CamelCaseName}).State = EntityState.Deleted;");
                s.Add($"");
                s.Add($"            await {CurrentEntity.Project.DbContextVariable}.SaveChangesAsync();");
            }
            s.Add($"");
            s.Add($"            return Ok();");
            s.Add($"        }}");
            s.Add($"");
            #endregion

            #region sort
            if (CurrentEntity.HasASortField)
            {
                s.Add($"        [HttpPost(\"sort\"){(CurrentEntity.AuthorizationType == AuthorizationType.None ? "" : ", AuthorizeRoles(Roles.Administrator)")}]");
                s.Add($"        public async Task<IActionResult> Sort([FromBody] Guid[] sortedIds)");
                s.Add($"        {{");
                // if it's a child entity, just sort the id's that were sent
                if (CurrentEntity.RelationshipsAsChild.Any(r => r.Hierarchy))
                {
                    s.Add($"            var {CurrentEntity.PluralName.ToCamelCase()} = await {CurrentEntity.Project.DbContextVariable}.{CurrentEntity.PluralName}");
                    s.Add($"                .Where(o => sortedIds.Contains(o.{CurrentEntity.KeyFields[0].Name}))");
                }
                else
                    s.Add($"            var {CurrentEntity.PluralName.ToCamelCase()} = await {CurrentEntity.Project.DbContextVariable}.{CurrentEntity.PluralName}");
                if (CurrentEntity.HasUserFilterField)
                    s.Add($"                .Where(o => o.{CurrentEntity.UserFilterFieldPath} == CurrentUser.{CurrentEntity.Project.UserFilterFieldName})");
                s.Add($"                .ToListAsync();");
                s.Add($"            if ({CurrentEntity.PluralName.ToCamelCase()}.Count != sortedIds.Length) return BadRequest(\"Some of the {CurrentEntity.PluralFriendlyName.ToLower()} could not be found\");");
                s.Add($"");
                //s.Add($"            var sortOrder = 0;");
                s.Add($"            foreach (var {CurrentEntity.Name.ToCamelCase()} in {CurrentEntity.PluralName.ToCamelCase()})");
                s.Add($"            {{");
                s.Add($"                {CurrentEntity.Project.DbContextVariable}.Entry({CurrentEntity.Name.ToCamelCase()}).State = EntityState.Modified;");
                s.Add($"                {CurrentEntity.Name.ToCamelCase()}.{CurrentEntity.SortField.Name} = {(CurrentEntity.SortField.SortDescending ? "sortedIds.Length - " : "")}Array.IndexOf(sortedIds, {CurrentEntity.Name.ToCamelCase()}.{CurrentEntity.KeyFields[0].Name});");
                //s.Add($"                sortOrder++;");
                s.Add($"            }}");
                s.Add($"");
                s.Add($"            await {CurrentEntity.Project.DbContextVariable}.SaveChangesAsync();");
                s.Add($"");
                s.Add($"            return Ok();");
                s.Add($"        }}");
                s.Add($"");
            }
            #endregion

            #region multiselect saves & deletes
            var processedEntityIds = new List<Guid>();
            foreach (var rel in CurrentEntity.RelationshipsAsParent.Where(o => o.UseMultiSelect && !o.ChildEntity.Exclude))
            {
                if (processedEntityIds.Contains(rel.ChildEntityId)) continue;
                processedEntityIds.Add(rel.ChildEntityId);

                var relationshipField = rel.RelationshipFields.Single();
                var reverseRel = rel.ChildEntity.RelationshipsAsChild.Where(o => o.RelationshipId != rel.RelationshipId).SingleOrDefault();
                var reverseRelationshipField = reverseRel.RelationshipFields.SingleOrDefault();
                if (reverseRelationshipField == null) throw new Exception($"{reverseRel.ParentEntity.Name} does not have a relationship field");

                s.Add($"        [HttpPost(\"{CurrentEntity.RoutePath}/{rel.ChildEntity.PluralName.ToLower()}\"){(CurrentEntity.AuthorizationType == AuthorizationType.None ? "" : ", AuthorizeRoles(Roles.Administrator)")}]");
                s.Add($"        public async Task<IActionResult> Save{rel.ChildEntity.PluralName}({CurrentEntity.ControllerParameters}, [FromBody] {rel.RelationshipFields.First().ParentField.NetType}[] {reverseRel.RelationshipFields.First().ParentField.Name.ToCamelCase()}s)");
                s.Add($"        {{");
                s.Add($"            if (!ModelState.IsValid) return BadRequest(ModelState);");
                s.Add($"");
                s.Add($"            var {rel.ChildEntity.PluralName.ToCamelCase()} = await db.{rel.ChildEntity.PluralName}");
                s.Add($"                .Where(o => o.{rel.RelationshipFields.First().ChildField.Name} == {rel.RelationshipFields.First().ParentField.Name.ToCamelCase()})");
                if (CurrentEntity.HasUserFilterField)
                    s.Add($"                .Where(o => o.{CurrentEntity.UserFilterFieldPath} == CurrentUser.{CurrentEntity.Project.UserFilterFieldName})");
                s.Add($"                .ToListAsync();");
                s.Add($"");
                s.Add($"            foreach (var {reverseRel.RelationshipFields.First().ChildField.Name.ToCamelCase()} in {reverseRel.RelationshipFields.First().ParentField.Name.ToCamelCase()}s)");
                s.Add($"            {{");
                s.Add($"                if (!{rel.ChildEntity.PluralName.ToCamelCase()}.Any(o => o.{reverseRel.RelationshipFields.First().ChildField.Name} == {reverseRel.RelationshipFields.First().ChildField.Name.ToCamelCase()}))");
                s.Add($"                {{");
                s.Add($"                    var {rel.ChildEntity.Name.ToCamelCase()} = new {rel.ChildEntity.Name} {{ {rel.RelationshipFields.First().ChildField.Name} = {rel.RelationshipFields.First().ParentField.Name.ToCamelCase()}, {reverseRel.RelationshipFields.First().ChildField.Name} = {reverseRel.RelationshipFields.First().ChildField.Name.ToCamelCase()} }};");
                s.Add($"                    db.Entry({rel.ChildEntity.Name.ToCamelCase()}).State = EntityState.Added;");
                s.Add($"                }}");
                s.Add($"            }}");
                s.Add($"");
                s.Add($"            await db.SaveChangesAsync();");
                s.Add($"");
                s.Add($"            return Ok();");
                s.Add($"        }}");
                s.Add($"");
            }
            #endregion

            #region deletes
            foreach (var rel in CurrentEntity.RelationshipsAsParent.Where(r => !r.ChildEntity.Exclude && r.DisplayListOnParent).OrderBy(r => r.SortOrder))
            {
                var entity = rel.ChildEntity;
                if (rel.UseMultiSelect)
                {
                    var reverseRel = rel.ChildEntity.RelationshipsAsChild.Where(o => o.RelationshipId != rel.RelationshipId).SingleOrDefault();
                    entity = reverseRel.ChildEntity;
                }

                s.Add($"        [HttpDelete(\"{CurrentEntity.RoutePath}/{rel.CollectionName.ToLower()}\"){(CurrentEntity.AuthorizationType == AuthorizationType.None ? "" : ", AuthorizeRoles(Roles.Administrator)")}]");
                s.Add($"        public async Task<IActionResult> Delete{rel.CollectionName}({CurrentEntity.ControllerParameters})");
                s.Add($"        {{");
                // might need to be fixed?
                s.Add($"            foreach (var {entity.Name.ToCamelCase()} in db.{entity.PluralName}.Where(o => {rel.RelationshipFields.Select(o => "o." + o.ChildField.Name + " == " + o.ParentField.Name.ToCamelCase()).Aggregate((current, next) => { return current + " && " + next; })}).ToList())");
                s.Add($"                db.Entry({entity.Name.ToCamelCase()}).State = EntityState.Deleted;");
                s.Add($"");
                s.Add($"            await db.SaveChangesAsync();");
                s.Add($"");
                s.Add($"            return Ok();");
                s.Add($"        }}");
                s.Add($"");
            }
            #endregion

            s.Add($"    }}");
            s.Add($"}}");

            return RunCodeReplacements(s.ToString(), CodeType.Controller);
        }

        public string GenerateBundleConfig()
        {
            var s = new StringBuilder();

            s.Add($"import {{ NgModule }} from '@angular/core';");
            s.Add($"import {{ CommonModule }} from '@angular/common';");
            s.Add($"import {{ FormsModule }} from '@angular/forms';");
            s.Add($"import {{ RouterModule }} from '@angular/router';");
            s.Add($"import {{ NgbModule }} from '@ng-bootstrap/ng-bootstrap';");
            s.Add($"import {{ DragDropModule }} from '@angular/cdk/drag-drop';");

            var entitiesToBundle = AllEntities.Where(e => !e.Exclude);
            var componentList = "";
            foreach (var e in entitiesToBundle)
            {
                s.Add($"import {{ {e.Name}ListComponent }} from './{e.PluralName.ToLower()}/{e.Name.ToLower()}.list.component';");
                s.Add($"import {{ {e.Name}EditComponent }} from './{e.PluralName.ToLower()}/{e.Name.ToLower()}.edit.component';");
                componentList += (componentList == "" ? "" : ", ") + $"{e.Name}ListComponent, {e.Name}EditComponent";

                //if (string.IsNullOrWhiteSpace(e.PreventAppSelectTypeScriptDeployment))
                //{
                //    s.Add($"import {{ {e.Name}SelectComponent }} from './{e.PluralName.ToLower()}/{e.Name.ToLower()}.select.component';");
                //    componentList += (componentList == "" ? "" : ", ") + $"{e.Name}SelectComponent";
                //}

                //if (string.IsNullOrWhiteSpace(e.PreventSelectModalTypeScriptDeployment))
                //{
                //    s.Add($"import {{ {e.Name}ModalComponent }} from './{e.PluralName.ToLower()}/{e.Name.ToLower()}.modal.component';");
                //    componentList += (componentList == "" ? "" : ", ") + $"{e.Name}ModalComponent";
                //}
            }
            s.Add($"import {{ SharedModule }} from './shared.module';");
            s.Add($"import {{ GeneratedRoutes }} from './generated.routes';");
            s.Add($"");


            s.Add($"@NgModule({{");
            s.Add($"   declarations: [{componentList}],");
            s.Add($"   imports: [");
            s.Add($"      CommonModule,");
            s.Add($"      FormsModule,");
            s.Add($"      RouterModule.forChild(GeneratedRoutes),");
            s.Add($"      NgbModule,");
            s.Add($"      DragDropModule,");
            s.Add($"      SharedModule");
            s.Add($"   ]");
            s.Add($"}})");
            s.Add($"export class GeneratedModule {{ }}");

            return RunCodeReplacements(s.ToString(), CodeType.BundleConfig);

        }

        public string GenerateSharedModule()
        {
            var s = new StringBuilder();

            s.Add($"import {{ NgModule }} from '@angular/core';");
            s.Add($"import {{ CommonModule }} from '@angular/common';");
            s.Add($"import {{ PagerComponent }} from './common/pager.component';");
            s.Add($"import {{ RouterModule }} from '@angular/router';");
            s.Add($"import {{ MainComponent }} from './main.component';");
            s.Add($"import {{ NavMenuComponent }} from './common/nav-menu/nav-menu.component';");
            s.Add($"import {{ MomentPipe }} from './common/pipes/momentPipe';");
            s.Add($"import {{ BooleanPipe }} from './common/pipes/booleanPipe';");
            s.Add($"import {{ FormsModule }} from '@angular/forms';");
            s.Add($"import {{ NgbModule }} from '@ng-bootstrap/ng-bootstrap';");
            s.Add($"import {{ DragDropModule }} from '@angular/cdk/drag-drop';");
            s.Add($"import {{ BreadcrumbModule }} from 'primeng/breadcrumb';");
            s.Add($"import {{ AppFileInputDirective }} from './common/directives/appfileinput';");
            s.Add($"import {{ AppHasRoleDirective }} from './common/directives/apphasrole';");

            var entitiesToBundle = AllEntities.Where(e => !e.Exclude);
            var componentList = "";
            foreach (var e in entitiesToBundle)
            {
                if (string.IsNullOrWhiteSpace(e.PreventAppSelectTypeScriptDeployment))
                {
                    s.Add($"import {{ {e.Name}SelectComponent }} from './{e.PluralName.ToLower()}/{e.Name.ToLower()}.select.component';");
                    componentList += "," + Environment.NewLine + $"        {e.Name}SelectComponent";
                }

                if (string.IsNullOrWhiteSpace(e.PreventSelectModalTypeScriptDeployment))
                {
                    s.Add($"import {{ {e.Name}ModalComponent }} from './{e.PluralName.ToLower()}/{e.Name.ToLower()}.modal.component';");
                    componentList += "," + Environment.NewLine + $"        {e.Name}ModalComponent";
                }
            }
            s.Add($"");


            s.Add($"@NgModule({{");
            //s.Add($"   declarations: [{componentList}],");
            s.Add($"    imports: [");
            s.Add($"        CommonModule,");
            s.Add($"        FormsModule,");
            s.Add($"        RouterModule,");
            s.Add($"        NgbModule,");
            s.Add($"        DragDropModule,");
            s.Add($"        BreadcrumbModule");
            s.Add($"    ],");
            s.Add($"    declarations: [");
            s.Add($"        PagerComponent,");
            s.Add($"        MainComponent,");
            s.Add($"        NavMenuComponent,");
            s.Add($"        MomentPipe,");
            s.Add($"        BooleanPipe,");
            s.Add($"        AppFileInputDirective,");
            s.Add($"        AppHasRoleDirective" + componentList);
            s.Add($"    ],");
            s.Add($"    exports: [");
            s.Add($"        PagerComponent,");
            s.Add($"        MainComponent,");
            s.Add($"        NavMenuComponent,");
            s.Add($"        NgbModule,");
            s.Add($"        MomentPipe,");
            s.Add($"        BooleanPipe,");
            s.Add($"        AppFileInputDirective,");
            s.Add($"        AppHasRoleDirective" + componentList);
            s.Add($"    ]");
            s.Add($"}})");
            s.Add($"export class SharedModule {{ }}");

            return RunCodeReplacements(s.ToString(), CodeType.SharedModule);

        }

        public string GenerateAppRouter()
        {
            var s = new StringBuilder();

            s.Add($"import {{ Route }} from '@angular/router';");
            s.Add($"import {{ AccessGuard }} from './common/auth/auth.accessguard';");

            var allEntities = AllEntities.Where(e => !e.Exclude).OrderBy(o => o.Name);
            foreach (var entity in allEntities)
            {
                s.Add($"import {{ {entity.Name}ListComponent }} from './{entity.PluralName.ToLower()}/{entity.Name.ToLower()}.list.component';");
                s.Add($"import {{ {entity.Name}EditComponent }} from './{entity.PluralName.ToLower()}/{entity.Name.ToLower()}.edit.component';");
            }
            //s.Add($"import {{ NotFoundComponent }} from './common/notfound.component';");
            s.Add($"");

            s.Add($"export const GeneratedRoutes: Route[] = [");

            foreach (var entity in allEntities.Where(o => !o.Exclude).OrderBy(o => o.Name))
            {
                var editOnRoot = !entity.RelationshipsAsChild.Any(r => r.Hierarchy);
                var childRelationships = entity.RelationshipsAsParent.Where(r => r.Hierarchy);

                s.Add($"    {{");
                s.Add($"        path: '{entity.PluralName.ToLower()}',");
                s.Add($"        canActivate: [AccessGuard],");
                s.Add($"        canActivateChild: [AccessGuard],");
                s.Add($"        component: {entity.Name}ListComponent,");
                s.Add($"        data: {{ breadcrumb: '{entity.PluralFriendlyName}' }}" + (editOnRoot ? "," : ""));
                if (editOnRoot)
                {
                    s.Add($"        children: [");
                    if (editOnRoot)
                    {
                        s.Add($"            {{");
                        s.Add($"                path: '{entity.KeyFields.Select(o => ":" + o.Name.ToCamelCase()).Aggregate((current, next) => { return current + "/" + next; })}',");
                        s.Add($"                component: {entity.Name}EditComponent,");
                        s.Add($"                canActivate: [AccessGuard],");
                        s.Add($"                canActivateChild: [AccessGuard],");
                        s.Add($"                data: {{");
                        s.Add($"                    breadcrumb: 'Add {entity.FriendlyName}'");
                        s.Add($"                }}" + (childRelationships.Any() ? "," : ""));
                    }
                    WriteChildRoutes(childRelationships, s, 0);
                    s.Add($"            }}");
                    s.Add($"        ]");
                }
                s.Add($"    }}" + (entity == allEntities.Last() ? "" : ","));
            }

            s.Add($"];");

            return RunCodeReplacements(s.ToString(), CodeType.AppRouter);

        }

        private void WriteChildRoutes(IEnumerable<Relationship> relationships, StringBuilder s, int level)
        {
            if (!relationships.Any()) return;
            var tabs = String.Concat(Enumerable.Repeat("        ", level));

            s.Add(tabs + $"                children: [");
            foreach (var relationship in relationships.Where(o => !o.ChildEntity.Exclude).OrderBy(o => o.ChildEntity.Name))
            {
                var entity = relationship.ChildEntity;
                var childRelationships = entity.RelationshipsAsParent.Where(r => r.Hierarchy);
                var keyfields = entity.GetNonHierarchicalKeyFields();

                s.Add(tabs + $"                    {{");
                s.Add(tabs + $"                        path: '{entity.PluralName.ToLower() + "/"}{keyfields.Select(o => ":" + o.Name.ToCamelCase()).Aggregate((current, next) => { return current + "/" + next; })}',");
                s.Add(tabs + $"                        component: {entity.Name}EditComponent,");
                s.Add(tabs + $"                        canActivate: [AccessGuard],");
                s.Add(tabs + $"                        canActivateChild: [AccessGuard],");
                s.Add(tabs + $"                        data: {{");
                s.Add(tabs + $"                            breadcrumb: 'Add {entity.FriendlyName}'");
                s.Add(tabs + $"                        }}" + (childRelationships.Any() ? "," : ""));
                if (childRelationships.Any())
                {
                    WriteChildRoutes(childRelationships, s, level + 1);
                }
                s.Add(tabs + $"                    }}" + (relationship == relationships.OrderBy(o => o.ChildEntity.Name).Last() ? "" : ","));
            }
            s.Add(tabs + $"                ]");
        }

        public string GenerateApiResource()
        {
            var s = new StringBuilder();

            var noKeysEntity = NormalEntities.FirstOrDefault(e => e.KeyFields.Count == 0);
            if (noKeysEntity != null)
                throw new InvalidOperationException(noKeysEntity.FriendlyName + " has no keys defined");

            s.Add($"import {{ environment }} from '../../../environments/environment';");
            s.Add($"import {{ Injectable }} from '@angular/core';");
            s.Add($"import {{ HttpClient, HttpParams }} from '@angular/common/http';");
            s.Add($"import {{ Observable }} from 'rxjs';");
            s.Add($"import {{ map }} from 'rxjs/operators';");
            s.Add($"import {{ {CurrentEntity.Name}, {CurrentEntity.Name}SearchOptions, {CurrentEntity.Name}SearchResponse }} from '../models/{CurrentEntity.Name.ToLower()}.model';");
            s.Add($"import {{ SearchQuery, PagingOptions }} from '../models/http.model';");
            s.Add($"");
            s.Add($"@Injectable({{ providedIn: 'root' }})");
            s.Add($"export class {CurrentEntity.Name}Service extends SearchQuery {{");
            s.Add($"");

            s.Add($"    constructor(private http: HttpClient) {{");
            s.Add($"        super();");
            s.Add($"    }}");
            s.Add($"");

            s.Add($"    search(params: {CurrentEntity.Name}SearchOptions): Observable<{CurrentEntity.Name}SearchResponse> {{");
            s.Add($"        const queryParams: HttpParams = this.buildQueryParams(params);");
            s.Add($"        return this.http.get(`${{environment.baseApiUrl}}{CurrentEntity.PluralName.ToLower()}`, {{ params: queryParams, observe: 'response' }})");
            s.Add($"            .pipe(");
            s.Add($"                map(response => {{");
            s.Add($"                    const headers = JSON.parse(response.headers.get(\"x-pagination\")) as PagingOptions;");
            s.Add($"                    const {CurrentEntity.PluralName.ToCamelCase()} = response.body as {CurrentEntity.Name}[];");
            s.Add($"                    return {{ {CurrentEntity.PluralName.ToCamelCase()}: {CurrentEntity.PluralName.ToCamelCase()}, headers: headers }};");
            s.Add($"                }})");
            s.Add($"            );");
            s.Add($"    }}");
            s.Add($"");

            var getParams = CurrentEntity.KeyFields.Select(o => o.Name.ToCamelCase() + ": " + o.JavascriptType).Aggregate((current, next) => current + ", " + next);
            var saveParams = CurrentEntity.Name.ToCamelCase() + ": " + CurrentEntity.Name;
            var getUrl = CurrentEntity.KeyFields.Select(o => "${" + o.Name.ToCamelCase() + "}").Aggregate((current, next) => current + "/" + next);
            var saveUrl = CurrentEntity.KeyFields.Select(o => "${" + CurrentEntity.Name.ToCamelCase() + "." + o.Name.ToCamelCase() + "}").Aggregate((current, next) => current + "/" + next);

            s.Add($"    get({getParams}): Observable<{CurrentEntity.Name}> {{");
            s.Add($"        return this.http.get<{CurrentEntity.Name}>(`${{environment.baseApiUrl}}{CurrentEntity.PluralName.ToLower()}/{getUrl}`);");
            s.Add($"    }}");
            s.Add($"");

            s.Add($"    save({saveParams}): Observable<{CurrentEntity.Name}> {{");
            s.Add($"        return this.http.post<{CurrentEntity.Name}>(`${{environment.baseApiUrl}}{CurrentEntity.PluralName.ToLower()}/{saveUrl}`, {CurrentEntity.Name.ToCamelCase()});");
            s.Add($"    }}");
            s.Add($"");

            s.Add($"    delete({getParams}): Observable<void> {{");
            s.Add($"        return this.http.delete<void>(`${{environment.baseApiUrl}}{CurrentEntity.PluralName.ToLower()}/{getUrl}`);");
            s.Add($"    }}");
            s.Add($"");

            if (CurrentEntity.HasASortField)
            {
                s.Add($"    sort(ids: {CurrentEntity.KeyFields.First().JavascriptType}[]): Observable<void> {{");
                s.Add($"        return this.http.post<void>(`${{environment.baseApiUrl}}{CurrentEntity.PluralName.ToLower()}/sort`, ids);");
                s.Add($"    }}");
                s.Add($"");
            }

            var processedEntities = new List<Guid>();
            foreach (var rel in CurrentEntity.RelationshipsAsParent.Where(o => o.UseMultiSelect && !o.ChildEntity.Exclude))
            {
                if (processedEntities.Contains(rel.ChildEntity.EntityId)) continue;
                processedEntities.Add(rel.ChildEntity.EntityId);

                var reverseRel = rel.ChildEntity.RelationshipsAsChild.Where(o => o.RelationshipId != rel.RelationshipId).SingleOrDefault();

                s.Add($"    save{rel.ChildEntity.PluralName}({rel.RelationshipFields.First().ParentField.Name.ToCamelCase()}: {rel.RelationshipFields.First().ParentField.JavascriptType}, {reverseRel.RelationshipFields.First().ParentField.Name.ToCamelCase()}s: {reverseRel.RelationshipFields.First().ParentField.JavascriptType}[]): Observable<void> {{");
                s.Add($"        return this.http.post<void>(`${{environment.baseApiUrl}}{CurrentEntity.PluralName.ToLower()}/{getUrl}/{rel.ChildEntity.PluralName.ToLower()}`, {reverseRel.RelationshipFields.First().ParentField.Name.ToCamelCase()}s);");
                s.Add($"    }}");
                s.Add($"");
            }

            foreach (var rel in CurrentEntity.RelationshipsAsParent.Where(r => !r.ChildEntity.Exclude && r.DisplayListOnParent).OrderBy(r => r.SortOrder))
            {
                s.Add($"    delete{rel.CollectionName}({rel.RelationshipFields.First().ParentField.Name.ToCamelCase()}: {rel.RelationshipFields.First().ParentField.JavascriptType}): Observable<void> {{");
                s.Add($"        return this.http.delete<void>(`${{environment.baseApiUrl}}{CurrentEntity.PluralName.ToLower()}/{getUrl}/{rel.CollectionName.ToLower()}`);");
                s.Add($"    }}");
                s.Add($"");
            }
            s.Add($"}}");

            return RunCodeReplacements(s.ToString(), CodeType.ApiResource);

        }

        public string GenerateListHtml()
        {
            var relationshipsAsParent = CurrentEntity.RelationshipsAsParent.Where(r => !r.ChildEntity.Exclude && r.DisplayListOnParent && r.Hierarchy).OrderBy(r => r.SortOrder);
            var hasChildRoutes = relationshipsAsParent.Any();
            if (!CurrentEntity.RelationshipsAsChild.Any(o => o.Hierarchy && !o.ParentEntity.Exclude))
                hasChildRoutes = true;

            var s = new StringBuilder();
            var t = "";
            if (hasChildRoutes)
            {
                s.Add($"<div *ngIf=\"route.children.length === 0\">");
                s.Add($"");
                t = "    ";
            }
            if (CurrentEntity.Fields.Any(f => f.SearchType != SearchType.None))
            {
                s.Add(t + $"<form (submit)=\"runSearch(0)\" novalidate>");
                s.Add($"");
                s.Add(t + $"    <div class=\"row g-3\">");
                s.Add($"");

                if (CurrentEntity.Fields.Any(f => f.SearchType == SearchType.Text))
                {
                    s.Add(t + $"        <div class=\"col-sm-6 col-md-4 col-lg-3\">");
                    s.Add(t + $"            <div class=\"form-group\">");
                    s.Add(t + $"                <input type=\"search\" name=\"q\" id=\"q\" [(ngModel)]=\"searchOptions.q\" max=\"100\" class=\"form-control\" placeholder=\"Search {CurrentEntity.PluralFriendlyName.ToLower()}\" />");
                    s.Add(t + $"            </div>");
                    s.Add(t + $"        </div>");
                    s.Add($"");
                }

                foreach (var field in CurrentEntity.Fields.Where(f => f.SearchType == SearchType.Exact).OrderBy(f => f.FieldOrder))
                {
                    if (field.CustomType == CustomType.Enum)
                    {
                        s.Add(t + $"        <div class=\"col-sm-6 col-md-4 col-lg-3\">");
                        s.Add(t + $"            <div class=\"form-group\">");
                        s.Add(t + $"                <select id=\"{field.Name.ToCamelCase()}\" name=\"{field.Name.ToCamelCase()}\" [(ngModel)]=\"searchOptions.{field.Name.ToCamelCase()}\" #{field.Name.ToCamelCase()}=\"ngModel\" class=\"form-control\">");
                        s.Add(t + $"                    <option *ngFor=\"let {field.Lookup.Name.ToCamelCase()} of {field.Lookup.PluralName.ToCamelCase()}\" [ngValue]=\"{field.Lookup.Name.ToCamelCase()}.value\">{{{{ {field.Lookup.Name.ToCamelCase()}.label }}}}</option>");
                        s.Add(t + $"                </select>");
                        s.Add(t + $"            </div>");
                        s.Add(t + $"        </div>");
                        s.Add($"");
                    }
                    else if (CurrentEntity.RelationshipsAsChild.Any(r => r.RelationshipFields.Any(f => f.ChildFieldId == field.FieldId)))
                    {
                        var relationship = CurrentEntity.GetParentSearchRelationship(field);
                        var parentEntity = relationship.ParentEntity;
                        var relField = relationship.RelationshipFields.Single();
                        if (true || relationship.UseSelectorDirective)
                        {
                            s.Add(t + $"        <div class=\"col-sm-6 col-md-4 col-lg-3\">");
                            s.Add(t + $"            <div class=\"form-group\">");
                            s.Add(t + $"                {relationship.AppSelector}");
                            s.Add(t + $"            </div>");
                            s.Add(t + $"        </div>");
                            s.Add($"");
                        }
                        else
                        {
                            //s.Add(t + $"        <div class=\"col-sm-6 col-md-4 col-lg-3\">");
                            //s.Add(t + $"            <div class=\"form-group\">");
                            //s.Add(t + $"                <ol id=\"{field.Name.ToCamelCase()}\" name=\"{field.Name.ToCamelCase()}\" title=\"{parentEntity.PluralFriendlyName}\" class=\"nya-bs-select form-control\" [(ngModel)]=\"searchOptions.{field.Name.ToCamelCase()}\" data-live-search=\"true\" data-size=\"10\">");
                            //s.Add(t + $"                    <li nya-bs-option=\"{parentEntity.Name.ToCamelCase()} in vm.{parentEntity.PluralName.ToCamelCase()}\" class=\"nya-bs-option{(CurrentEntity.Project.Bootstrap3 ? "" : " dropdown-item")}\" data-value=\"{parentEntity.Name.ToCamelCase()}.{relField.ParentField.Name.ToCamelCase()}\">");
                            //s.Add(t + $"                        <a>{{{{{parentEntity.Name.ToCamelCase()}.{relationship.ParentField.Name.ToCamelCase()}}}}}<span class=\"fas fa-check check-mark\"></span></a>");
                            //s.Add(t + $"                    </li>");
                            //s.Add(t + $"                </ol>");
                            //s.Add(t + $"            </div>");
                            //s.Add(t + $"        </div>");
                            //s.Add($"");
                        }
                    }
                }
                foreach (var field in CurrentEntity.Fields.Where(f => f.SearchType == SearchType.Range))
                {
                    if (field.CustomType == CustomType.Date)
                    {
                        s.Add(t + $"        <div class=\"col-sm-6 col-md-3 col-lg-2\">");
                        s.Add(t + $"            <div class=\"form-group\" ngbTooltip=\"From Date\" container=\"body\" placement=\"top\">");
                        s.Add(t + $"                <div class=\"input-group\">");
                        s.Add(t + $"                    <input type=\"text\" id=\"from{field.Name}\" name=\"from{field.Name}\" [(ngModel)]=\"searchOptions.from{field.Name}\" #from{field.Name}=\"ngModel\" class=\"form-control\" readonly placeholder=\"yyyy-mm-dd\" ngbDatepicker #dpFrom{field.Name}=\"ngbDatepicker\" tabindex=\"-1\" (click)=\"dpFrom{field.Name}.toggle()\" />");
                        s.Add(t + $"                    <div class=\"input-group-append\">");
                        s.Add(t + $"                        <button class=\"btn btn-secondary calendar\" (click)=\"dpFrom{field.Name}.toggle()\" type=\"button\"><i class=\"fas fa-calendar-alt\"></i></button>");
                        s.Add(t + $"                    </div>");
                        s.Add(t + $"                </div>");
                        s.Add(t + $"            </div>");
                        s.Add(t + $"        </div>");
                        s.Add($"");
                        s.Add(t + $"        <div class=\"col-sm-6 col-md-3 col-lg-2\">");
                        s.Add(t + $"            <div class=\"form-group\" ngbTooltip=\"To Date\" container=\"body\" placement=\"top\">");
                        s.Add(t + $"                <div class=\"input-group\">");
                        s.Add(t + $"                    <input type=\"text\" id=\"to{field.Name}\" name=\"to{field.Name}\" [(ngModel)]=\"searchOptions.to{field.Name}\" #to{field.Name}=\"ngModel\" class=\"form-control\" readonly placeholder=\"yyyy-mm-dd\" ngbDatepicker #dpTo{field.Name}=\"ngbDatepicker\" tabindex=\"-1\" (click)=\"dpTo{field.Name}.toggle()\" />");
                        s.Add(t + $"                    <div class=\"input-group-append\">");
                        s.Add(t + $"                        <button class=\"btn btn-secondary calendar\" (click)=\"dpTo{field.Name}.toggle()\" type=\"button\"><i class=\"fas fa-calendar-alt\"></i></button>");
                        s.Add(t + $"                    </div>");
                        s.Add(t + $"                </div>");
                        s.Add(t + $"            </div>");
                        s.Add(t + $"        </div>");
                        s.Add($"");
                    }
                }
                s.Add(t + $"    </div>");
                s.Add($"");
                s.Add(t + $"    <fieldset class=\"my-3\">");
                s.Add($"");
                s.Add(t + $"        <button type=\"submit\" class=\"btn btn-success\">Search<i class=\"fas fa-search ms-1\"></i></button>");
                if (CurrentEntity.RelationshipsAsChild.Count(r => r.Hierarchy) == 0)
                {
                    // todo: needs field list + field.newParameter
                    s.Add(t + $"        <a [routerLink]=\"['./', 'add']\" class=\"btn btn-primary ms-1\">Add<i class=\"fas fa-plus-circle ms-1\"></i></a>");
                }
                s.Add($"");
                s.Add(t + $"    </fieldset>");
                s.Add($"");
                s.Add(t + $"</form>");
            }
            s.Add($"");
            s.Add(t + $"<hr />");
            s.Add($"");
            // removed (not needed?): id=\"resultsList\" 
            var useSortColumn = CurrentEntity.HasASortField && !CurrentEntity.RelationshipsAsChild.Any(r => r.Hierarchy);

            s.Add(t + $"<table class=\"table table-striped table-hover table-sm row-navigation\">");
            s.Add(t + $"    <thead>");
            s.Add(t + $"        <tr>");
            if (useSortColumn)
                s.Add(t + $"            <th class=\"text-center fa-col-width\" *ngIf=\"{CurrentEntity.PluralName.ToCamelCase()}.length > 1\"><i class=\"fas fa-sort\"></i></th>");
            foreach (var field in CurrentEntity.Fields.Where(f => f.ShowInSearchResults).OrderBy(f => f.FieldOrder))
                s.Add(t + $"            <th>{field.Label}</th>");
            s.Add(t + $"        </tr>");
            s.Add(t + $"    </thead>");
            s.Add(t + $"    <tbody{(useSortColumn ? " cdkDropList (cdkDropListDropped)=\"sort($event)\"" : "")}>");
            s.Add(t + $"        <tr *ngFor=\"let {CurrentEntity.CamelCaseName} of {CurrentEntity.PluralName.ToCamelCase()}\" (click)=\"goTo{CurrentEntity.Name}({CurrentEntity.CamelCaseName})\"{(useSortColumn ? " cdkDrag" : "")}>");
            if (useSortColumn)
                s.Add(t + $"            <td class=\"text-center fa-col-width\" cdkDragHandle (click)=\"$event.stopPropagation();\" *ngIf=\"{CurrentEntity.PluralName.ToCamelCase()}.length > 1\"><span *cdkDragPreview></span><i class=\"fas fa-sort\"></i></td>");
            foreach (var field in CurrentEntity.Fields.Where(f => f.ShowInSearchResults).OrderBy(f => f.FieldOrder))
            {
                s.Add(t + $"            <td>{field.ListFieldHtml}</td>");
            }
            s.Add(t + $"        </tr>");
            s.Add(t + $"    </tbody>");
            s.Add(t + $"</table>");
            s.Add($"");
            // entities with sort fields need to show all (pageSize = 0) for sortability, so no paging needed
            if (!(CurrentEntity.HasASortField && !CurrentEntity.RelationshipsAsChild.Any(r => r.Hierarchy)))
            {
                s.Add(t + $"<pager [headers]=\"headers\" (pageChanged)=\"runSearch($event)\"></pager>");
                s.Add($"");
            }
            if (hasChildRoutes)
            {
                s.Add($"</div>");
                s.Add($"");
                s.Add($"<router-outlet></router-outlet>");
            }

            return RunCodeReplacements(s.ToString(), CodeType.ListHtml);
        }

        public string GenerateListTypeScript()
        {
            bool includeEntities = false;
            if (CurrentEntity.RelationshipsAsChild.Any(r => r.Hierarchy))
                includeEntities = true;
            else
                foreach (var field in CurrentEntity.Fields.Where(f => f.ShowInSearchResults).OrderBy(f => f.FieldOrder))
                    if (CurrentEntity.RelationshipsAsChild.Any(r => r.RelationshipFields.Any(f => f.ChildFieldId == field.FieldId)))
                    {
                        includeEntities = true;
                        break;
                    }
            var enumLookups = CurrentEntity.Fields.Where(o => o.FieldType == FieldType.Enum && (o.ShowInSearchResults || o.SearchType == SearchType.Exact)).Select(o => o.Lookup).Distinct().ToList();
            var relationshipsAsParent = CurrentEntity.RelationshipsAsParent.Where(r => !r.ChildEntity.Exclude && r.DisplayListOnParent && r.Hierarchy).OrderBy(r => r.SortOrder);
            var hasChildRoutes = relationshipsAsParent.Any();
            if (!CurrentEntity.RelationshipsAsChild.Any(o => o.Hierarchy && !o.ParentEntity.Exclude))
                hasChildRoutes = true;

            var s = new StringBuilder();

            s.Add($"import {{ Component, OnInit{(hasChildRoutes ? ", OnDestroy" : "")} }} from '@angular/core';");
            s.Add($"import {{ Router, ActivatedRoute{(hasChildRoutes ? ", NavigationEnd" : "")} }} from '@angular/router';");
            s.Add($"import {{ Subject{(hasChildRoutes ? ", Subscription" : "")} }} from 'rxjs';");
            s.Add($"import {{ PagingOptions }} from '../common/models/http.model';");
            if (CurrentEntity.HasASortField)
                s.Add($"import {{ CdkDragDrop, moveItemInArray }} from '@angular/cdk/drag-drop';");
            s.Add($"import {{ ErrorService }} from '../common/services/error.service';");
            s.Add($"import {{ {CurrentEntity.Name}SearchOptions, {CurrentEntity.Name}SearchResponse, {CurrentEntity.Name} }} from '../common/models/{CurrentEntity.Name.ToLower()}.model';");
            s.Add($"import {{ {CurrentEntity.Name}Service }} from '../common/services/{CurrentEntity.Name.ToLower()}.service';");
            if (enumLookups.Any())
                s.Add($"import {{ Enum, Enums }} from '../common/models/enums.model';");
            if (CurrentEntity.HasASortField)
                s.Add($"import {{ ToastrService }} from 'ngx-toastr';");
            s.Add($"");
            s.Add($"@Component({{");
            s.Add($"    selector: '{CurrentEntity.Name.ToLower()}-list',");
            s.Add($"    templateUrl: './{CurrentEntity.Name.ToLower()}.list.component.html'");
            s.Add($"}})");
            s.Add($"export class {CurrentEntity.Name}ListComponent implements OnInit {{");
            s.Add($"");
            s.Add($"    public {CurrentEntity.PluralName.ToCamelCase()}: {CurrentEntity.Name}[] = [];");
            s.Add($"    public searchOptions = new {CurrentEntity.Name}SearchOptions();");
            s.Add($"    public headers = new PagingOptions();");
            if (hasChildRoutes)
                s.Add($"    private routerSubscription: Subscription;");
            foreach (var enumLookup in enumLookups)
                s.Add($"    public {enumLookup.PluralName.ToCamelCase()}: Enum[] = Enums.{enumLookup.PluralName};");

            s.Add($"");
            s.Add($"    constructor(");
            s.Add($"        public route: ActivatedRoute,");
            s.Add($"        private router: Router,");
            s.Add($"        private errorService: ErrorService,");
            if (CurrentEntity.HasASortField)
                s.Add($"        private toastr: ToastrService,");
            s.Add($"        private {CurrentEntity.Name.ToCamelCase()}Service: {CurrentEntity.Name}Service");
            s.Add($"    ) {{");
            s.Add($"    }}");
            s.Add($"");
            s.Add($"    ngOnInit(): void {{");
            if (includeEntities)
                s.Add($"        this.searchOptions.includeEntities = true;");
            if (CurrentEntity.HasASortField && !CurrentEntity.RelationshipsAsChild.Any(r => r.Hierarchy))
                s.Add($"        this.searchOptions.pageSize = 0;");
            if (hasChildRoutes)
            {
                s.Add($"        this.routerSubscription = this.router.events.subscribe(event => {{");
                s.Add($"            if (event instanceof NavigationEnd && !this.route.firstChild) {{");
                s.Add($"                this.runSearch();");
                s.Add($"            }}");
                s.Add($"        }});");
            }
            s.Add($"        this.runSearch();");
            s.Add($"    }}");
            s.Add($"");
            if (hasChildRoutes)
            {
                s.Add($"    ngOnDestroy(): void {{");
                s.Add($"        this.routerSubscription.unsubscribe();");
                s.Add($"    }}");
                s.Add($"");
            }
            s.Add($"    runSearch(pageIndex = 0): Subject<{CurrentEntity.Name}SearchResponse> {{");
            s.Add($"");
            s.Add($"        this.searchOptions.pageIndex = pageIndex;");
            s.Add($"");
            s.Add($"        const subject = new Subject<{CurrentEntity.Name}SearchResponse>();");
            s.Add($"");
            s.Add($"        this.{CurrentEntity.Name.ToCamelCase()}Service.search(this.searchOptions)");
            s.Add($"            .subscribe(");
            s.Add($"                response => {{");
            s.Add($"                    subject.next(response);");
            s.Add($"                    this.{CurrentEntity.PluralName.ToCamelCase()} = response.{CurrentEntity.PluralName.ToCamelCase()};");
            s.Add($"                    this.headers = response.headers;");
            s.Add($"                }},");
            s.Add($"                err => {{");
            s.Add($"                    this.errorService.handleError(err, \"{CurrentEntity.PluralFriendlyName}\", \"Load\");");
            s.Add($"                }}");
            s.Add($"            );");
            s.Add($"");
            s.Add($"        return subject;");
            s.Add($"");
            s.Add($"    }}");
            s.Add($"");
            if (CurrentEntity.HasASortField)
            {
                s.Add($"    sort(event: CdkDragDrop<{CurrentEntity.Name}[]>) {{");
                s.Add($"        moveItemInArray(this.{CurrentEntity.PluralName.ToCamelCase()}, event.previousIndex, event.currentIndex);");
                s.Add($"        this.{CurrentEntity.Name.ToCamelCase()}Service.sort(this.{CurrentEntity.PluralName.ToCamelCase()}.map(o => o.{CurrentEntity.KeyFields.First().Name.ToCamelCase()})).subscribe(");
                s.Add($"            () => {{");
                s.Add($"                this.toastr.success(\"The sort order has been updated\", \"Change Sort Order\");");
                s.Add($"            }},");
                s.Add($"            err => {{");
                s.Add($"                this.errorService.handleError(err, \"{CurrentEntity.PluralFriendlyName}\", \"Sort\");");
                s.Add($"            }});");
                s.Add($"    }}");
                s.Add($"");
            }
            s.Add($"    goTo{CurrentEntity.Name}({CurrentEntity.Name.ToCamelCase()}: {CurrentEntity.Name}): void {{");
            s.Add($"        this.router.navigate({GetRouterLink(CurrentEntity, CurrentEntity)});");
            s.Add($"    }}");
            s.Add($"}}");
            s.Add($"");

            return RunCodeReplacements(s.ToString(), CodeType.ListTypeScript);
        }

        private string GetRouterLink(Entity entity, Entity sourceEntity)
        {
            // reason for change!!
            // change to use relative routes, so I can inject a url prefix (start app at www.site.com/app/<here>)
            // note: this requirement can't work with base href=/app, as that gets set for the entire site, where 
            // the ktu-covid project was effectively 2 websites in 1: /offline and /admin - both needing base href=/

            string routerLink = string.Empty;

            if (entity.RelationshipsAsChild.Any(r => r.Hierarchy))
            {
                var hierarchicalRelationship = entity.RelationshipsAsChild.Where(o => o.Hierarchy).Single();

                if (hierarchicalRelationship.ParentEntityId == sourceEntity.EntityId)
                {

                    // the navigating from entity is the relationship parent - it just needs a relative link:
                    var keyFields = entity.GetNonHierarchicalKeyFields();
                    routerLink = $"[\"{entity.PluralName.ToLower()}\", {keyFields.Select(o => $"{entity.Name.ToCamelCase()}.{o.Name.ToCamelCase()}").Aggregate((current, next) => { return current + ", " + next; })}], {{ relativeTo: this.route }}";

                }
                else
                {
                    var prefix = string.Empty;

                    while (hierarchicalRelationship != null)
                    {

                        var child = hierarchicalRelationship.ChildEntity;
                        var parent = hierarchicalRelationship.ParentEntity;
                        var keyFields = child.GetNonHierarchicalKeyFields();
                        var keyFieldsRoute = keyFields.Select(o => $"{prefix + child.Name.ToCamelCase()}.{o.Name.ToCamelCase()}").Aggregate((current, next) => { return current + ", " + next; });

                        routerLink = $"\"{child.PluralName.ToLower()}\", " + keyFieldsRoute + (routerLink == string.Empty ? "" : ", ") + routerLink;

                        prefix += hierarchicalRelationship.ChildEntity.Name.ToCamelCase() + ".";

                        hierarchicalRelationship = parent.RelationshipsAsChild.SingleOrDefault(r => r.Hierarchy);

                        if (hierarchicalRelationship == null)
                        {
                            keyFields = parent.GetNonHierarchicalKeyFields();
                            keyFieldsRoute = keyFields.Select(o => $"{prefix + parent.Name.ToCamelCase()}.{o.Name.ToCamelCase()}").Aggregate((current, next) => { return current + ", " + next; });
                            routerLink = $"[\"/{parent.PluralName.ToLower()}\", " + keyFieldsRoute + ", " + routerLink + "]";
                        }

                    }
                }
            }
            else
            {
                //routerLink = $"'/{entity.PluralName.ToLower()}', { entity.KeyFields.Select(o => entity.Name.ToCamelCase() + "." + o.Name.ToCamelCase()).Aggregate((current, next) => current + ", " + next) }";
                if (CurrentEntity == entity)
                    routerLink = $"[{entity.KeyFields.Select(o => entity.Name.ToCamelCase() + "." + o.Name.ToCamelCase()).Aggregate((current, next) => current + ", " + next)}], {{ relativeTo: this.route }}";
                else
                    routerLink = $"[\"/{entity.PluralName.ToLower()}\", {entity.KeyFields.Select(o => entity.Name.ToCamelCase() + "." + o.Name.ToCamelCase()).Aggregate((current, next) => current + ", " + next)}]";
            }

            return routerLink;
        }

        public string GenerateEditHtml()
        {
            var relationshipsAsParent = CurrentEntity.RelationshipsAsParent.Where(r => !r.ChildEntity.Exclude && r.DisplayListOnParent && r.Hierarchy).OrderBy(r => r.SortOrder);
            var hasChildRoutes = relationshipsAsParent.Any() || CurrentEntity.UseChildRoutes;

            var s = new StringBuilder();

            var t = string.Empty;
            if (hasChildRoutes)
            {
                s.Add($"<div *ngIf=\"route.children.length === 0\">");
                s.Add($"");
                t = "    ";
            }
            s.Add(t + $"<form name=\"form\" (submit)=\"save(form)\" novalidate #form=\"ngForm\" [ngClass]=\"{{ 'was-validated': form.submitted }}\">");
            s.Add($"");
            s.Add(t + $"    <fieldset class=\"group\">");
            s.Add($"");
            s.Add(t + $"        <legend>{CurrentEntity.FriendlyName}</legend>");
            s.Add($"");
            s.Add(t + $"        <div class=\"row g-3\">");
            s.Add($"");

            // not really a bootstrap3 issue - old projects will be affected by this now being commented
            //if (CurrentEntity.Project.Bootstrap3)
            //{
            //    s.Add(t + $"            <div class=\"col-sm-12\">");
            //    s.Add($"");
            //    t = "    ";
            //}
            #region form fields
            foreach (var field in CurrentEntity.Fields.OrderBy(o => o.FieldOrder))
            {
                if (field.KeyField && field.CustomType != CustomType.String && !CurrentEntity.HasCompositePrimaryKey) continue;
                if (field.EditPageType == EditPageType.Exclude) continue;
                if (field.EditPageType == EditPageType.SortField) continue;
                if (field.EditPageType == EditPageType.CalculatedField) continue;

                var isAppSelect = CurrentEntity.RelationshipsAsChild.Any(r => r.RelationshipFields.Any(f => f.ChildFieldId == field.FieldId)) && CurrentEntity.GetParentSearchRelationship(field).UseSelectorDirective;

                if (CurrentEntity.RelationshipsAsChild.Any(r => r.RelationshipFields.Any(f => f.ChildFieldId == field.FieldId)))
                {
                    var relationship = CurrentEntity.GetParentSearchRelationship(field);
                    //var relationshipField = relationship.RelationshipFields.Single(f => f.ChildFieldId == field.FieldId);
                    if (relationship.Hierarchy) continue;
                }

                var fieldName = field.Name.ToCamelCase();

                // todo: allow an override in the user fields?
                var controlSize = "col-sm-6 col-md-4";
                var tagType = "input";
                var attributes = new Dictionary<string, string>();
                var ngIf = string.Empty;

                attributes.Add("id", fieldName);
                attributes.Add("name", fieldName);
                attributes.Add("class", "form-control");

                // default = text
                attributes.Add("type", "text");

                var readOnly = field.EditPageType == EditPageType.ReadOnly || field.EditPageType == EditPageType.FileName;

                if (!readOnly || isAppSelect)
                    attributes.Add("[(ngModel)]", CurrentEntity.Name.ToCamelCase() + "." + fieldName);

                if (!readOnly)
                {
                    attributes.Add("#" + fieldName, "ngModel");
                    if (!field.IsNullable)
                        attributes.Add("required", null);
                    if (field.FieldId == CurrentEntity.PrimaryFieldId)
                        attributes.Add("(ngModelChange)", $"changeBreadcrumb()");
                    if (field.CustomType == CustomType.Number && field.Scale > 0)
                        attributes.Add("step", "any");

                    if (field.EditPageType == EditPageType.EditWhenNew) attributes.Add("[disabled]", "!isNew");

                    if (field.CustomType == CustomType.Number)
                    {
                        attributes["type"] = "number";
                    }
                    else if (field.CustomType == CustomType.Enum)
                    {
                        tagType = "select";
                        attributes.Remove("type");
                    }
                    else if (field.FieldType == FieldType.Date || field.FieldType == FieldType.SmallDateTime || field.FieldType == FieldType.DateTime)
                    {
                        attributes.Add("readonly", null);
                        //if (field.EditPageType == EditPageType.ReadOnly) attributes.Add("disabled", null);
                        attributes.Add("placeholder", "yyyy-mm-dd");
                        attributes.Add("ngbDatepicker", null);
                        attributes.Add("#dp" + field.Name, "ngbDatepicker");
                        attributes.Add("tabindex", "-1");
                        attributes.Add("(click)", "dp" + field.Name + ".toggle()");
                    }
                    else
                    {
                        if (!CurrentEntity.RelationshipsAsChild.Any(r => r.RelationshipFields.Any(f => f.ChildFieldId == field.FieldId) && r.UseSelectorDirective))
                        {
                            if (field.Length > 0) attributes.Add("maxlength", field.Length.ToString());
                            if (field.MinLength > 0) attributes.Add("minlength", field.MinLength.ToString());
                        }
                        if (field.RegexValidation != null)
                            attributes.Add("pattern", field.RegexValidation);
                    }
                }
                else
                {
                    // read only field properties:
                    attributes.Add("readonly", null);
                    if (!isAppSelect)
                    {
                        if (CurrentEntity.RelationshipsAsChild.Any(r => r.RelationshipFields.Any(f => f.ChildFieldId == field.FieldId)))
                        {
                            var relationship = CurrentEntity.GetParentSearchRelationship(field);
                            attributes.Add("value", "{{" + CurrentEntity.Name.ToCamelCase() + "." + relationship.ParentName.ToCamelCase() + "?." + relationship.ParentEntity.PrimaryField.Name.ToCamelCase() + "}}");
                        }
                        else if (field.FieldType == FieldType.Enum)
                            attributes.Add("value", $"{{{{{field.Lookup.PluralName.ToCamelCase()}[{CurrentEntity.Name.ToCamelCase()}.{fieldName.ToCamelCase()}]?.label}}}}");
                        else if (field.CustomType == CustomType.Date)
                            attributes.Add("value", $"{{{{{CurrentEntity.Name.ToCamelCase()}.{fieldName.ToCamelCase()} | momentPipe: '{field.DateFormatString}'}}}}");
                        else if (field.FieldType == FieldType.Money)
                            attributes.Add("value", $"{{{{{CurrentEntity.Name.ToCamelCase()}.{fieldName.ToCamelCase()} | currency }}}}");
                        else
                            attributes.Add("value", $"{{{{{CurrentEntity.Name.ToCamelCase()}.{fieldName.ToCamelCase()}}}}}");
                    }
                    else
                    {
                        attributes.Add("disabled", null);
                    }
                }

                if (field.CustomType == CustomType.Boolean)
                {
                    attributes["type"] = "checkbox";
                    attributes.Remove("required");
                    attributes["class"] = "form-check-input";
                }
                else if (field.CustomType == CustomType.String && (field.FieldType == FieldType.Text || field.FieldType == FieldType.nText || field.Length == 0))
                {
                    tagType = "textarea";
                    attributes.Remove("type");
                    attributes.Add("rows", "5");
                }
                else if (field.EditPageType == EditPageType.FileContents)
                {
                    //ngIf = " *ngIf=\"isNew\"";
                    field.Label = "File"; //set label to 'File' (don't save!)
                    attributes["type"] = "file";
                    attributes.Add("app-file-input", null);
                    // make ngModel output only so it doesn't error trying to write to the input value (not allowed in html)
                    attributes.Remove("[(ngModel)]");
                    attributes.Add("(ngModel)", CurrentEntity.Name.ToCamelCase() + "." + fieldName);
                    attributes.Add("[(appFileContent)]", CurrentEntity.Name.ToCamelCase() + "." + fieldName);
                    var fileNameField = CurrentEntity.Fields.SingleOrDefault(o => o.EditPageType == EditPageType.FileName);
                    if (fileNameField != null) attributes.Add("[(appFileName)]", CurrentEntity.Name.ToCamelCase() + "." + fileNameField.Name.ToCamelCase());

                }
                else if (field.EditPageType == EditPageType.FileName)
                {
                    ngIf = " *ngIf=\"!isNew\"";
                }




                if (CurrentEntity.RelationshipsAsChild.Any(r => r.RelationshipFields.Any(f => f.ChildFieldId == field.FieldId)))
                {
                    var relationship = CurrentEntity.GetParentSearchRelationship(field);
                    var relationshipField = relationship.RelationshipFields.Single(f => f.ChildFieldId == field.FieldId);
                    if (!relationship.UseSelectorDirective)
                    {
                        // this was only for Read only's - needs to go above
                        //if (field.EditPageType == EditPageType.ReadOnly)
                        //{
                        //    attributes.Remove("[(ngModel)]");
                        //    attributes.Add("value", "{{" + CurrentEntity.Name.ToCamelCase() + "." + relationship.ParentName.ToCamelCase() + "?." + relationship.ParentEntity.PrimaryField.Name.ToCamelCase() + "}}");
                        //}
                    }
                    else if (!relationship.Hierarchy)
                    {
                        tagType = relationship.ParentEntity.Name.Hyphenated() + "-select";
                        if (attributes.ContainsKey("type")) attributes.Remove("type");
                        if (attributes.ContainsKey("class")) attributes.Remove("class");
                        attributes.Add($"[({relationship.ParentEntity.Name.ToCamelCase()})]", $"{relationship.ChildEntity.Name.ToCamelCase()}.{relationship.ParentName.ToCamelCase()}");
                    }
                }

                s.Add(t + $"            <div class=\"{controlSize}\"{ngIf}>");
                s.Add(t + $"                <div class=\"form-group\"{(readOnly ? "" : $" [ngClass]=\"{{ 'is-invalid': {fieldName}.invalid }}\"")}>");
                s.Add($"");
                s.Add(t + $"                    <label for=\"{fieldName.ToCamelCase()}\">");
                s.Add(t + $"                        {field.Label}:");
                s.Add(t + $"                    </label>");
                s.Add($"");

                var controlHtml = $"<{tagType}";
                foreach (var attribute in attributes)
                {
                    controlHtml += " " + attribute.Key;
                    if (attribute.Value != null) controlHtml += $"=\"{attribute.Value}\"";
                }
                if (tagType == "input")
                    controlHtml += " />";
                else if (tagType == "select")
                {
                    controlHtml += $">" + Environment.NewLine;
                    controlHtml += t + $"                        <option *ngFor=\"let {field.Lookup.Name.ToCamelCase()} of {field.Lookup.PluralName.ToCamelCase()}\" [ngValue]=\"{field.Lookup.Name.ToCamelCase()}.value\">{{{{ {field.Lookup.Name.ToCamelCase()}.label }}}}</option>" + Environment.NewLine;
                    controlHtml += t + $"                    </{tagType}>";
                }
                //";
                else
                    controlHtml += $"></{tagType}>";

                if (attributes.ContainsKey("type") && attributes["type"] == "checkbox")
                {
                    s.Add(t + $"                    <div class=\"form-check\">");
                    s.Add(t + $"                        {controlHtml}");
                    s.Add(t + $"                        <label class=\"form-check-label\" for=\"{field.Name.ToCamelCase()}\">");
                    s.Add(t + $"                            {field.Label}");
                    s.Add(t + $"                        </label>");
                    s.Add(t + $"                    </div>");
                }
                else if (field.CustomType == CustomType.Date && !readOnly)
                {
                    s.Add(t + $"                    <div class=\"input-group\">");
                    s.Add(t + $"                        {controlHtml}");
                    s.Add(t + $"                        <div class=\"input-group-append\">");
                    s.Add(t + $"                            <button class=\"btn btn-secondary calendar\" (click)=\"dp{field.Name}.toggle()\" type=\"button\"><i class=\"fas fa-calendar-alt\"></i></button>");
                    s.Add(t + $"                        </div>");
                    s.Add(t + $"                    </div>");
                }
                else if (field.FieldType == FieldType.VarBinary && field.EditPageType == EditPageType.FileContents)
                {
                    var fileNameField = CurrentEntity.Fields.FirstOrDefault(o => o.EditPageType == EditPageType.FileName);
                    if (fileNameField == null) throw new Exception(CurrentEntity.Name + ": FileContents field doesn't have a matching FileName field");

                    s.Add(t + $"                    <div class=\"input-group\">");
                    s.Add(t + $"                        <div class=\"input-group-prepend\" *ngIf=\"!isNew\">");
                    s.Add(t + $"                            <button type=\"button\" class=\"btn btn-primary\" (click)=\"download()\"><i class=\"fa fa-fw fa-cloud-download-alt\"></i></button>");
                    s.Add(t + $"                        </div>");
                    s.Add(t + $"                        <div class=\"custom-file\">");
                    s.Add(t + $"                            {controlHtml}");
                    s.Add(t + $"                            <label class=\"custom-file-label\" for=\"{field.Name.ToCamelCase()}\">{{{{{CurrentEntity.Name.ToCamelCase()}.{fileNameField.Name.ToCamelCase()} || \"Choose file\"}}}}</label>");
                    s.Add(t + $"                        </div>");
                    s.Add(t + $"                    </div>");
                }
                else
                    s.Add(t + $"                    {controlHtml}");


                s.Add($"");

                if (!readOnly)
                {
                    var validationErrors = new Dictionary<string, string>();
                    if (!field.IsNullable && field.CustomType != CustomType.Boolean && field.EditPageType != EditPageType.ReadOnly) validationErrors.Add("required", $"{field.Label} is required");
                    if (!CurrentEntity.RelationshipsAsChild.Any(r => r.RelationshipFields.Any(f => f.ChildFieldId == field.FieldId) && r.UseSelectorDirective))
                    {
                        if (field.MinLength > 0) validationErrors.Add("minlength", $"{field.Label} must be at least {field.MinLength} characters long");
                        if (field.Length > 0) validationErrors.Add("maxlength", $"{field.Label} must be at most {field.Length} characters long");
                    }
                    if (field.RegexValidation != null) validationErrors.Add("pattern", $"{field.Label} does not match the specified pattern");

                    foreach (var validationError in validationErrors)
                    {
                        s.Add(t + $"                    <div *ngIf=\"{fieldName}.errors?.{validationError.Key}\" class=\"invalid-feedback\">");
                        s.Add(t + $"                        {validationError.Value}");
                        s.Add(t + $"                    </div>");
                        s.Add($"");
                    }
                }

                s.Add(t + $"                </div>");
                s.Add(t + $"            </div>");
                s.Add($"");

            }
            #endregion

            if (CurrentEntity.EntityType == EntityType.User)
            {
                s.Add(t + $"            <div class=\"col-sm-6 col-md-4\">");
                s.Add(t + $"                <div class=\"form-group\">");
                s.Add($"");
                s.Add(t + $"                    <label>");
                s.Add(t + $"                        Roles:");
                s.Add(t + $"                    </label>");
                s.Add($"");
                s.Add(t + $"                    <select id=\"roles\" name=\"roles\" [multiple]=\"true\" class=\"form-control\" [(ngModel)]=\"user.roles\">");
                s.Add(t + $"                        <option *ngFor=\"let role of roles\" [ngValue]=\"role.name\">{{{{role.label}}}}</option>");
                s.Add(t + $"                    </select>");
                s.Add($"");
                s.Add(t + $"                </div>");
                s.Add(t + $"            </div>");
                s.Add($"");
            }

            s.Add(t + $"        </div>");
            s.Add($"");
            s.Add(t + $"    </fieldset>");
            s.Add($"");

            s.Add(t + $"    <fieldset class=\"my-3\">");
            s.Add(t + $"        <button type=\"submit\" class=\"btn btn-success\">Save<i class=\"fas fa-check ms-1\"></i></button>");
            s.Add(t + $"        <button type=\"button\" *ngIf=\"!isNew\" class=\"btn btn-outline-danger ms-1\" (click)=\"delete()\">Delete<i class=\"fas fa-times ms-1\"></i></button>");
            s.Add(t + $"    </fieldset>");
            s.Add($"");
            s.Add(t + $"</form>");

            #region child lists
            if (CurrentEntity.RelationshipsAsParent.Any(r => !r.ChildEntity.Exclude && r.DisplayListOnParent))
            {
                var relationships = CurrentEntity.RelationshipsAsParent.Where(r => !r.ChildEntity.Exclude && r.DisplayListOnParent).OrderBy(r => r.SortOrder);
                var counter = 0;

                s.Add($"");
                s.Add(t + $"<div *ngIf=\"!isNew\">");
                s.Add($"");
                s.Add(t + $"    <hr />");
                s.Add($"");
                s.Add(t + $"    <nav ngbNav #nav=\"ngbNav\" class=\"nav-tabs\">");
                s.Add($"");
                foreach (var relationship in relationships)
                {
                    counter++;

                    s.Add(t + $"        <ng-container ngbNavItem>");
                    s.Add($"");
                    s.Add(t + $"            <a ngbNavLink>{relationship.CollectionFriendlyName}</a>");
                    s.Add($"");
                    s.Add(t + $"            <ng-template ngbNavContent>");
                    s.Add($"");


                    var childEntity = relationship.ChildEntity;

                    if (relationship.UseMultiSelect)
                    {
                        s.Add(t + $"                <button class=\"btn btn-primary my-3\" (click)=\"add{relationship.CollectionName}()\">Add {relationship.CollectionFriendlyName}<i class=\"fas fa-plus-circle ms-1\"></i></button><br />");
                        s.Add($"");
                    }
                    else if (relationship.Hierarchy)
                    {
                        // trying to get this to work for instances like African POT Project->Team hierarchy, where I only want 1 add for the userId
                        s.Add(t + $"                <a [routerLink]=\"['./{childEntity.PluralName.ToLower()}', 'add']\" class=\"btn btn-primary my-3\">Add {childEntity.FriendlyName}<i class=\"fas fa-plus-circle ms-1\"></i></a><br />");

                        s.Add($"");
                    }

                    #region table
                    s.Add(t + $"                <table class=\"table table-striped table-hover table-sm row-navigation\">");
                    s.Add(t + $"                    <thead>");
                    s.Add(t + $"                        <tr>");
                    if (relationship.Hierarchy && childEntity.HasASortField)
                        s.Add(t + $"                            <th *ngIf=\"{relationship.CollectionName.ToCamelCase()}.length > 1\" class=\"text-center fa-col-width\"><i class=\"fas fa-sort mt-1\"></i></th>");
                    if (relationship.UseMultiSelect)
                    {
                        var reverseRel = relationship.ChildEntity.RelationshipsAsChild.Where(o => o.RelationshipId != relationship.RelationshipId).SingleOrDefault();

                        s.Add(t + $"                            <th>{reverseRel.ParentFriendlyName}</th>");
                    }
                    else
                    {
                        foreach (var column in childEntity.GetSearchResultsFields(CurrentEntity))
                        {
                            s.Add(t + $"                            <th>{column.Header}</th>");
                        }
                    }
                    s.Add(t + $"                            <th class=\"fa-col-width text-center\"><i class=\"fas fa-times clickable\" (click)=\"deleteAll{relationship.CollectionName}()\" ngbTooltip=\"Delete all {relationship.CollectionFriendlyName.ToLower()}\" container=\"body\" placement=\"left\"></i></th>");
                    s.Add(t + $"                        </tr>");
                    s.Add(t + $"                    </thead>");
                    if (relationship.Hierarchy && childEntity.HasASortField)
                    {
                        s.Add(t + $"                    <tbody cdkDropList (cdkDropListDropped)=\"sort{relationship.CollectionName}($event)\">");
                        s.Add(t + $"                        <tr *ngFor=\"let {childEntity.Name.ToCamelCase()} of {relationship.CollectionName.ToCamelCase()}\" (click)=\"goTo{childEntity.Name}({childEntity.Name.ToCamelCase()})\" cdkDrag>");
                        s.Add(t + $"                            <td *ngIf=\"{relationship.CollectionName.ToCamelCase()}.length > 1\" class=\"text-center fa-col-width\" cdkDragHandle (click)=\"$event.stopPropagation();\"><span *cdkDragPreview></span><i class=\"fas fa-sort sortable-handle mt-1\" (click)=\"$event.stopPropagation();\"></i></td>");
                    }
                    else
                    {
                        s.Add(t + $"                    <tbody>");
                        s.Add(t + $"                        <tr *ngFor=\"let {childEntity.Name.ToCamelCase()} of {relationship.CollectionName.ToCamelCase()}\" (click)=\"goTo{childEntity.Name}({childEntity.Name.ToCamelCase()})\">");
                    }
                    // this was added for TrainTrack entityLinks; not sure how it will affect other projects!
                    if (relationship.UseMultiSelect)
                    {
                        var reverseRel = relationship.ChildEntity.RelationshipsAsChild.Where(o => o.RelationshipId != relationship.RelationshipId).SingleOrDefault();

                        s.Add(t + $"                            <td>{{{{ {relationship.ChildEntity.Name.ToCamelCase()}.{reverseRel.ParentName.ToCamelCase()}.{reverseRel.ParentEntity.PrimaryField.Name.ToCamelCase()} }}}}</td>");
                    }
                    else
                    {
                        foreach (var column in childEntity.GetSearchResultsFields(CurrentEntity))
                        {
                            s.Add(t + $"                            <td>{column.Value}</td>");
                        }
                    }
                    s.Add(t + $"                            <td class=\"text-center\"><i class=\"fas fa-times clickable p-1 text-danger\" (click)=\"delete{relationship.CollectionName}({relationship.ChildEntity.Name.ToCamelCase()}, $event)\"></i></td>");
                    s.Add(t + $"                        </tr>");
                    s.Add(t + $"                    </tbody>");
                    s.Add(t + $"                </table>");
                    s.Add($"");
                    if (!relationship.ChildEntity.HasASortField)
                    {
                        s.Add(t + $"                <pager [headers]=\"{relationship.CollectionName.ToCamelCase()}Headers\" (pageChanged)=\"load{relationship.CollectionName}($event)\"></pager>");
                        s.Add($"");
                    }
                    #endregion

                    // entities with sort fields need to show all (pageSize = 0) for sortability, so no paging needed
                    if (!childEntity.HasASortField)
                    {
                        //s.Add(t + $"            <div class=\"row\">");
                        //s.Add(t + $"                <div class=\"col-sm-7\">");
                        //s.Add(t + $"                   <{CurrentEntity.Project.AngularDirectivePrefix}-pager headers=\"{relationship.ChildEntity.PluralName.ToCamelCase()}Headers\" callback=\"load{relationship.CollectionName}\"></{CurrentEntity.Project.AngularDirectivePrefix}-pager>");
                        //s.Add(t + $"                </div>");
                        //s.Add(t + $"                <div class=\"col-sm-5 text-right resultsInfo\">");
                        //s.Add(t + $"                   <{CurrentEntity.Project.AngularDirectivePrefix}-pager-info headers=\"{relationship.ChildEntity.PluralName.ToCamelCase()}Headers\"></{CurrentEntity.Project.AngularDirectivePrefix}-pager-info>");
                        //s.Add(t + $"                </div>");
                        //s.Add(t + $"            </div>");
                        //s.Add($"");
                    }

                    s.Add(t + $"            </ng-template>");
                    s.Add($"");
                    s.Add(t + $"        </ng-container>");
                    s.Add($"");
                }
                s.Add(t + $"    </nav>");
                s.Add($"");
                s.Add(t + $"    <div [ngbNavOutlet]=\"nav\" class=\"mt-1\"></div>");
                s.Add($"");
                s.Add(t + $"</div>");
            }
            #endregion

            if (hasChildRoutes)
            {
                s.Add($"");
                s.Add($"</div>");
            }

            foreach (var rel in CurrentEntity.RelationshipsAsParent.Where(o => o.UseMultiSelect && !o.ChildEntity.Exclude))
            {
                var reverseRel = rel.ChildEntity.RelationshipsAsChild.Where(o => o.RelationshipId != rel.RelationshipId).SingleOrDefault();

                //[organisation]=\"user.organisation\"   [canRemoveFilters]=\"false\"
                s.Add($"");
                s.Add($"<{reverseRel.ParentEntity.Name.Hyphenated()}-modal #{reverseRel.ParentEntity.Name.ToCamelCase()}Modal (changes)=\"change{reverseRel.ParentEntity.Name}($event)\" [multiple]=\"true\"></{reverseRel.ParentEntity.Name.Hyphenated()}-modal>");
            }

            if (hasChildRoutes)
            {
                s.Add($"");
                s.Add($"<router-outlet></router-outlet>");
            }

            return RunCodeReplacements(s.ToString(), CodeType.EditHtml);
        }

        public string GenerateEditTypeScript()
        {
            var multiSelectRelationships = CurrentEntity.RelationshipsAsParent.Where(r => r.UseMultiSelect && !r.ChildEntity.Exclude).OrderBy(o => o.SortOrder);
            var relationshipsAsParent = CurrentEntity.RelationshipsAsParent.Where(r => !r.ChildEntity.Exclude && r.DisplayListOnParent).OrderBy(r => r.SortOrder);
            var relationshipsAsChildHierarchy = CurrentEntity.RelationshipsAsChild.FirstOrDefault(r => r.Hierarchy);
            var enumLookups = CurrentEntity.Fields.Where(o => o.FieldType == FieldType.Enum).OrderBy(o => o.FieldOrder).Select(o => o.Lookup).Distinct().ToList();
            var nonHKeyFields = CurrentEntity.GetNonHierarchicalKeyFields();
            // add lookups for table/search fields on children entities
            foreach (var rel in CurrentEntity.RelationshipsAsParent.Where(r => !r.ChildEntity.Exclude && r.DisplayListOnParent).OrderBy(r => r.SortOrder))
                foreach (var field in rel.ChildEntity.Fields.Where(o => o.ShowInSearchResults))
                    if (field.FieldType == FieldType.Enum && !enumLookups.Contains(field.Lookup))
                        enumLookups.Add(field.Lookup);
            // this is for the breadcrumb being on the parent entity which has a primary field of type enum
            var addEnum = CurrentEntity.RelationshipsAsChild.SingleOrDefault(r => r.RelationshipFields.Count == 1 && r.RelationshipFields.First().ChildFieldId == CurrentEntity.PrimaryField.FieldId)?.ParentEntity?.PrimaryField?.FieldType == FieldType.Enum;
            if (!addEnum)
            {
                // can't get this to work: 
                // intention: on CurrentEntity, a child entity list has a field that is a relationship-link to another 
                if (CurrentEntity.RelationshipsAsParent.Any(o => (o.DisplayListOnParent || o.Hierarchy) && o.ChildEntity.Fields.Any(f => f.ShowInSearchResults && f.RelationshipFieldsAsChild.Any(rf => rf.ParentField.Entity.PrimaryField.FieldType == FieldType.Enum))))
                    addEnum = true;
            }

            var hasChildRoutes = relationshipsAsParent.Any(o => o.Hierarchy) || CurrentEntity.UseChildRoutes;
            var hasFileContents = CurrentEntity.Fields.Any(o => o.EditPageType == EditPageType.FileContents && o.CustomType == CustomType.Binary);

            var s = new StringBuilder();

            s.Add($"import {{ Component, OnInit{(hasChildRoutes ? ", OnDestroy" : "") + (multiSelectRelationships.Any() ? ", ViewChild" : "")} }} from '@angular/core';");
            s.Add($"import {{ Router, ActivatedRoute{(hasChildRoutes ? ", NavigationEnd" : "")} }} from '@angular/router';");
            s.Add($"import {{ ToastrService }} from 'ngx-toastr';");
            s.Add($"import {{ NgForm }} from '@angular/forms';");
            if (relationshipsAsParent.Any())// || CurrentEntity.EntityType == EntityType.User)
                s.Add($"import {{ Subject{(hasChildRoutes || CurrentEntity.EntityType == EntityType.User ? ", Subscription" : "")} }} from 'rxjs';");
            s.Add($"import {{ HttpErrorResponse }} from '@angular/common/http';");
            s.Add($"import {{ BreadcrumbService }} from 'angular-crumbs';");
            s.Add($"import {{ ErrorService }} from '../common/services/error.service';");
            if (relationshipsAsParent.Any())
                s.Add($"import {{ PagingOptions }} from '../common/models/http.model';");
            s.Add($"import {{ {CurrentEntity.Name} }} from '../common/models/{CurrentEntity.Name.ToLower()}.model';");
            s.Add($"import {{ {CurrentEntity.Name}Service }} from '../common/services/{CurrentEntity.Name.ToLower()}.service';");
            if (enumLookups.Count > 0 || addEnum)
                s.Add($"import {{ Enum, Enums{(CurrentEntity.PrimaryField.FieldType == FieldType.Enum ? ", " + CurrentEntity.PrimaryField.Lookup.PluralName : "")} }} from '../common/models/enums.model';");
            foreach (var relChildEntity in relationshipsAsParent.Select(o => o.ChildEntity).Distinct().OrderBy(o => o.Name))
            {
                s.Add($"import {{ {(relChildEntity.EntityId == CurrentEntity.EntityId ? "" : relChildEntity.Name + ", ")}{relChildEntity.Name}SearchOptions, {relChildEntity.Name}SearchResponse }} from '../common/models/{relChildEntity.Name.ToLower()}.model';");
                if (relChildEntity.EntityId != CurrentEntity.EntityId)
                    s.Add($"import {{ {relChildEntity.Name}Service }} from '../common/services/{relChildEntity.Name.ToLower()}.service';");
            }
            if (CurrentEntity.EntityType == EntityType.User)
            {
                s.Add($"import {{ Roles, Role }} from '../common/models/roles.model';");
                s.Add($"import {{ ProfileModel }} from '../common/models/profile.models';");
                s.Add($"import {{ ProfileService }} from '../common/services/profile.service';");
            }
            var processedEntityIds = new List<Guid>();
            foreach (var rel in multiSelectRelationships)
            {
                if (processedEntityIds.Contains(rel.ChildEntityId)) continue;
                processedEntityIds.Add(rel.ChildEntityId);

                var reverseRel = rel.ChildEntity.RelationshipsAsChild.Where(o => o.RelationshipId != rel.RelationshipId).SingleOrDefault();

                s.Add($"import {{ {reverseRel.ParentEntity.Name}ModalComponent }} from '../{reverseRel.ParentEntity.PluralName.ToLower()}/{reverseRel.ParentEntity.Name.ToLower()}.modal.component';");
                if (reverseRel.ParentEntity != CurrentEntity) s.Add($"import {{ {reverseRel.ParentEntity.Name} }} from '../common/models/{reverseRel.ParentEntity.Name.ToLower()}.model';");
            }
            if (relationshipsAsParent.Any(o => o.Hierarchy && o.ChildEntity.HasASortField))
                s.Add($"import {{ moveItemInArray, CdkDragDrop }} from '@angular/cdk/drag-drop';");
            if (CurrentEntity.PrimaryField.CustomType == CustomType.Date)
                s.Add($"import * as moment from 'moment';");
            if (hasFileContents)
                s.Add($"import {{ DownloadService }} from '../common/services/download.service';");

            s.Add($"");

            s.Add($"@Component({{");
            s.Add($"    selector: '{CurrentEntity.Name.ToLower()}-edit',");
            s.Add($"    templateUrl: './{CurrentEntity.Name.ToLower()}.edit.component.html'");
            s.Add($"}})");

            s.Add($"export class {CurrentEntity.Name}EditComponent implements OnInit{(hasChildRoutes ? ", OnDestroy" : "")} {{");
            s.Add($"");
            s.Add($"    public {CurrentEntity.Name.ToCamelCase()}: {CurrentEntity.Name} = new {CurrentEntity.Name}();");
            s.Add($"    public isNew = true;");
            if (hasChildRoutes)
                s.Add($"    private routerSubscription: Subscription;");
            foreach (var enumLookup in enumLookups)
                s.Add($"    public {enumLookup.PluralName.ToCamelCase()}: Enum[] = Enums.{enumLookup.PluralName};");
            if (CurrentEntity.EntityType == EntityType.User)
            {
                s.Add($"    public roles: Role[] = Roles.List;");
                s.Add($"    private profile: ProfileModel;");
            }
            //foreach(var x in CurrentEntity.RelationshipsAsParent.Where(o => (o.DisplayListOnParent || o.Hierarchy) && o.ChildEntity.Fields.Any(f => f.ShowInSearchResults && f.RelationshipFieldsAsChild.Any(rf => rf.ParentField.Entity.PrimaryField.FieldType == FieldType.Enum)
            s.Add($"");
            foreach (var rel in relationshipsAsParent)
            {
                s.Add($"    private {rel.CollectionName.ToCamelCase()}SearchOptions = new {rel.ChildEntity.Name}SearchOptions();");
                s.Add($"    public {rel.CollectionName.ToCamelCase()}Headers = new PagingOptions();");
                s.Add($"    public {rel.CollectionName.ToCamelCase()}: {rel.ChildEntity.Name}[] = [];");
                s.Add($"");
            }
            processedEntityIds.Clear();
            foreach (var rel in multiSelectRelationships)
            {
                if (processedEntityIds.Contains(rel.ChildEntityId)) continue;
                processedEntityIds.Add(rel.ChildEntityId);

                var reverseRel = rel.ChildEntity.RelationshipsAsChild.Where(o => o.RelationshipId != rel.RelationshipId).SingleOrDefault();
                s.Add($"    @ViewChild('{reverseRel.ParentEntity.Name.ToCamelCase()}Modal') {reverseRel.ParentEntity.Name.ToCamelCase()}Modal: {reverseRel.ParentEntity.Name}ModalComponent;");
            }
            if (multiSelectRelationships.Any())
                s.Add($"");

            s.Add($"    constructor(");
            s.Add($"        private router: Router,");
            s.Add($"        {(hasChildRoutes ? "public" : "private")} route: ActivatedRoute,");
            s.Add($"        private toastr: ToastrService,");
            s.Add($"        private breadcrumbService: BreadcrumbService,");
            s.Add($"        private {CurrentEntity.Name.ToCamelCase()}Service: {CurrentEntity.Name}Service,");
            var relChildEntities = relationshipsAsParent.Where(o => o.ChildEntityId != CurrentEntity.EntityId).Select(o => o.ChildEntity).Distinct().OrderBy(o => o.Name);
            foreach (var relChildEntity in relChildEntities)
                s.Add($"        private {relChildEntity.Name.ToCamelCase()}Service: {relChildEntity.Name}Service,");
            if (CurrentEntity.EntityType == EntityType.User)
                s.Add($"        private profileService: ProfileService,");
            if (hasFileContents)
                s.Add($"        private downloadService: DownloadService,");

            s.Add($"        private errorService: ErrorService");
            s.Add($"    ) {{");
            s.Add($"    }}");
            s.Add($"");

            s.Add($"    ngOnInit(): void {{");
            s.Add($"");

            if (CurrentEntity.EntityType == EntityType.User)
            {
                s.Add($"        this.profileService.getProfile().subscribe(profile => {{");
                s.Add($"            this.profile = profile;");
                s.Add($"        }});");
                s.Add($"");
            }

            // use subscribe, so that a save changes url and reloads data
            s.Add($"        this.route.params.subscribe(params => {{");
            s.Add($"");
            //if (relationshipsAsChildHierarchy != null)
            //    foreach (var rfield in relationshipsAsChildHierarchy.RelationshipFields)
            //s.Add($"            const {rfield.ParentField.Name.ToCamelCase()} = this.route.snapshot.parent.params.{rfield.ParentField.Name.ToCamelCase()};");

            foreach (var keyField in nonHKeyFields)
            {
                s.Add($"            const {keyField.Name.ToCamelCase()} = params[\"{keyField.Name.ToCamelCase()}\"];");
            }
            if (relationshipsAsChildHierarchy != null)
            {
                foreach (var rfield in relationshipsAsChildHierarchy.RelationshipFields)
                    s.Add($"            this.{CurrentEntity.Name.ToCamelCase()}.{rfield.ChildField.Name.ToCamelCase()} = this.route.snapshot.parent.params.{rfield.ParentField.Name.ToCamelCase()};");
            }
            s.Add($"            this.isNew = {CurrentEntity.GetNonHierarchicalKeyFields().Select(o => o.Name.ToCamelCase() + " === \"add\"").Aggregate((current, next) => { return current + " && " + next; })};");

            s.Add($"");

            s.Add($"            if (!this.isNew) {{");
            s.Add($"");
            foreach (var keyField in nonHKeyFields)
            {
                s.Add($"                this.{CurrentEntity.Name.ToCamelCase()}.{keyField.Name.ToCamelCase()} = {keyField.Name.ToCamelCase()};");
            }
            s.Add($"                this.load{CurrentEntity.Name}();");
            s.Add($"");
            foreach (var rel in relationshipsAsParent)
            {
                foreach (var relField in rel.RelationshipFields)
                    s.Add($"                this.{rel.CollectionName.ToCamelCase()}SearchOptions.{relField.ChildField.Name.ToCamelCase()} = {relField.ParentField.Name.ToCamelCase()};");
                s.Add($"                this.{rel.CollectionName.ToCamelCase()}SearchOptions.includeEntities = true;");
                s.Add($"                this.load{rel.CollectionName}();");
                s.Add($"");
            }

            s.Add($"            }}");
            s.Add($"");
            if (hasChildRoutes)
            {
                s.Add($"            this.routerSubscription = this.router.events.subscribe(event => {{");
                s.Add($"                if (event instanceof NavigationEnd && !this.route.firstChild) {{");
                // this was causing a 404 error after deleting
                //s.Add($"                  this.load{CurrentEntity.Name}();");
                // 
                s.Add($"                    // this will double-load on new save, as params change (above) + nav ends");
                foreach (var rel in relationshipsAsParent.Where(o => o.Hierarchy))
                {
                    s.Add($"                    this.load{rel.CollectionName}();");
                }
                s.Add($"                }}");
                s.Add($"            }});");
                s.Add($"");
            }

            s.Add($"        }});");
            s.Add($"");
            s.Add($"    }}");
            s.Add($"");

            if (hasChildRoutes)
            {
                s.Add($"    ngOnDestroy(): void {{");
                s.Add($"        this.routerSubscription.unsubscribe();");
                s.Add($"    }}");
                s.Add($"");
            }

            s.Add($"    private load{CurrentEntity.Name}(): void {{");
            s.Add($"");
            s.Add($"        this.{CurrentEntity.Name.ToCamelCase()}Service.get({CurrentEntity.KeyFields.Select(o => $"this.{CurrentEntity.Name.ToCamelCase()}.{o.Name.ToCamelCase()}").Aggregate((current, next) => { return current + ", " + next; })})");
            s.Add($"            .subscribe(");
            s.Add($"                {CurrentEntity.Name.ToCamelCase()} => {{");
            s.Add($"                    this.{CurrentEntity.Name.ToCamelCase()} = {CurrentEntity.Name.ToCamelCase()};");
            s.Add($"                    this.changeBreadcrumb();");
            s.Add($"                }},");
            s.Add($"                err => {{");
            s.Add($"                    this.errorService.handleError(err, \"{CurrentEntity.FriendlyName}\", \"Load\");");
            s.Add($"                    if (err instanceof HttpErrorResponse && err.status === 404)");
            s.Add($"                        {CurrentEntity.ReturnRoute}");
            s.Add($"                }}");
            s.Add($"            );");
            s.Add($"");
            s.Add($"    }}");
            s.Add($"");

            s.Add($"    save(form: NgForm): void {{");
            s.Add($"");
            s.Add($"        if (form.invalid) {{");
            s.Add($"");
            s.Add($"            this.toastr.error(\"The form has not been completed correctly.\", \"Form Error\");");
            s.Add($"            return;");
            s.Add($"");
            s.Add($"        }}");
            s.Add($"");
            s.Add($"        this.{CurrentEntity.Name.ToCamelCase()}Service.save(this.{CurrentEntity.Name.ToCamelCase()})");
            s.Add($"            .subscribe(");
            s.Add($"                {(CurrentEntity.ReturnOnSave ? "()" : CurrentEntity.Name.ToCamelCase())} => {{");
            s.Add($"                    this.toastr.success(\"The {CurrentEntity.FriendlyName.ToLower()} has been saved\", \"Save {CurrentEntity.FriendlyName}\");");
            if (CurrentEntity.ReturnOnSave)
                s.Add($"                    {CurrentEntity.ReturnRoute}");
            else
            {
                if (hasChildRoutes)
                {
                    s.Add($"                    if (this.isNew) {{");
                    s.Add($"                        this.ngOnDestroy();");
                    s.Add($"                        this.router.navigate([\"../\", {nonHKeyFields.Select(o => $"{CurrentEntity.Name.ToCamelCase()}.{o.Name.ToCamelCase()}").Aggregate((current, next) => { return current + ", " + next; })}], {{ relativeTo: this.route }});");
                    s.Add($"                    }}");
                }
                else
                {
                    s.Add($"                    if (this.isNew) this.router.navigate([\"../\", {nonHKeyFields.Select(o => $"{CurrentEntity.Name.ToCamelCase()}.{o.Name.ToCamelCase()}").Aggregate((current, next) => { return current + ", " + next; })}], {{ relativeTo: this.route }});");
                }
            }
            if (CurrentEntity.EntityType == EntityType.User)
            {
                s.Add($"                    else {{");
                s.Add($"                        // reload profile if editing self");
                s.Add($"                        if (this.user.id === this.profile.userId)");
                s.Add($"                            this.profileService.getProfile(true).subscribe();");
                s.Add($"                    }}");
            }
            //else if (!CurrentEntity.ReturnOnSave)
            //{
            //    s.Add($"                    }}");
            //}
            s.Add($"                }},");
            s.Add($"                err => {{");
            s.Add($"                    this.errorService.handleError(err, \"{CurrentEntity.FriendlyName}\", \"Save\");");
            s.Add($"                }}");
            s.Add($"            );");
            s.Add($"");
            s.Add($"    }}");
            s.Add($"");

            s.Add($"    delete(): void {{");
            s.Add($"");
            // todo: make this a modal?
            s.Add($"        if (!confirm(\"Confirm delete?\")) return;");
            s.Add($"");
            s.Add($"        this.{CurrentEntity.Name.ToCamelCase()}Service.delete({CurrentEntity.KeyFields.Select(o => $"this.{CurrentEntity.Name.ToCamelCase()}.{o.Name.ToCamelCase()}").Aggregate((current, next) => { return current + ", " + next; })})");
            s.Add($"            .subscribe(");
            s.Add($"                () => {{");
            s.Add($"                    this.toastr.success(\"The {CurrentEntity.FriendlyName.ToLower()} has been deleted\", \"Delete {CurrentEntity.FriendlyName}\");");
            s.Add($"                    {CurrentEntity.ReturnRoute}");
            s.Add($"                }},");
            s.Add($"                err => {{");
            s.Add($"                    this.errorService.handleError(err, \"{CurrentEntity.FriendlyName}\", \"Delete\");");
            s.Add($"                }}");
            s.Add($"            );");
            s.Add($"");
            s.Add($"    }}");
            s.Add($"");

            s.Add($"    changeBreadcrumb(): void {{");
            // if the 'primary field' is a foreign key to another entity
            //if()//CurrentEntity.PrimaryFieldId
            if (CurrentEntity.RelationshipsAsChild.Any(r => r.RelationshipFields.Count == 1 && r.RelationshipFields.First()?.ChildFieldId == CurrentEntity.PrimaryField.FieldId))
            {
                var rel = CurrentEntity.RelationshipsAsChild.Single(r => r.RelationshipFields.Count == 1 && r.RelationshipFields.First().ChildFieldId == CurrentEntity.PrimaryField.FieldId);
                var primaryField = rel.ParentEntity.PrimaryField;
                if (primaryField.CustomType == CustomType.Enum)
                {
                    s.Add($"        this.breadcrumbService.changeBreadcrumb(this.route.snapshot, this.{CurrentEntity.Name.ToCamelCase()}.{rel.ParentEntity.Name.ToCamelCase()}.{primaryField.Name.ToCamelCase()} !== undefined ? Enums.{primaryField.Lookup.PluralName}[this.{CurrentEntity.Name.ToCamelCase()}.{rel.ParentEntity.Name.ToCamelCase()}.{primaryField.Name.ToCamelCase()}].label?.substring(0, 25) : \"(new {CurrentEntity.FriendlyName.ToLower()})\");");
                }
                else
                {
                    s.Add($"        this.breadcrumbService.changeBreadcrumb(this.route.snapshot, this.{CurrentEntity.Name.ToCamelCase()}.{rel.RelationshipFields.First().ChildField.Name.ToCamelCase()} ? this.{CurrentEntity.Name.ToCamelCase()}.{rel.ParentName.ToCamelCase()}?.{primaryField.Name.ToCamelCase() + (primaryField.JavascriptType == "string" ? "" : "?.toString()")}?.substring(0, 25) : \"(new {CurrentEntity.FriendlyName.ToLower()})\");");
                }
            }
            else if (CurrentEntity.PrimaryField.CustomType == CustomType.Date)
            {
                s.Add($"        this.breadcrumbService.changeBreadcrumb(this.route.snapshot, this.{CurrentEntity.Name.ToCamelCase()}.{CurrentEntity.PrimaryField.Name.ToCamelCase()} ? moment(this.{CurrentEntity.Name.ToCamelCase()}.{CurrentEntity.PrimaryField?.Name.ToCamelCase()}).format(\"LL\") : \"(new {CurrentEntity.FriendlyName.ToLower()})\");");
            }
            else if (CurrentEntity.PrimaryField.CustomType == CustomType.Enum)
            {
                s.Add($"        this.breadcrumbService.changeBreadcrumb(this.route.snapshot, this.{CurrentEntity.Name.ToCamelCase()}.{CurrentEntity.PrimaryField.Name.ToCamelCase()} !== undefined ? Enums.{CurrentEntity.PrimaryField.Lookup.PluralName}[this.{CurrentEntity.Name.ToCamelCase()}.{CurrentEntity.PrimaryField.Name.ToCamelCase()}].label?.substring(0, 25) : \"(new {CurrentEntity.FriendlyName.ToLower()})\");");
            }
            else
            {
                s.Add($"        this.breadcrumbService.changeBreadcrumb(this.route.snapshot, this.{CurrentEntity.Name.ToCamelCase()}.{CurrentEntity.PrimaryField.Name.ToCamelCase()} !== undefined ? this.{CurrentEntity.Name.ToCamelCase()}.{CurrentEntity.PrimaryField?.Name.ToCamelCase() + (CurrentEntity.PrimaryField?.JavascriptType == "string" ? "" : ".toString()")}.substring(0, 25) : \"(new {CurrentEntity.FriendlyName.ToLower()})\");");
            }
            s.Add($"    }}");
            s.Add($"");

            if (hasFileContents)
            {
                s.Add($"    download(): void {{");
                s.Add($"        this.downloadService.download{CurrentEntity.Name}({CurrentEntity.KeyFields.Select(o => $"this.{CurrentEntity.Name.ToCamelCase()}.{o.Name.ToCamelCase()}").Aggregate((current, next) => { return current + ", " + next; })}).subscribe();");
                s.Add($"    }}");
                s.Add($"");

            }

            foreach (var rel in relationshipsAsParent)
            {
                if (!rel.DisplayListOnParent && !rel.Hierarchy) continue;

                s.Add($"    load{rel.CollectionName}({(rel.ChildEntity.HasASortField ? "" : "pageIndex = 0")}): Subject<{rel.ChildEntity.Name}SearchResponse> {{");
                s.Add($"");

                if (rel.ChildEntity.HasASortField)
                    s.Add($"        this.{rel.CollectionName.ToCamelCase()}SearchOptions.pageSize = 0;");
                else
                    s.Add($"        this.{rel.CollectionName.ToCamelCase()}SearchOptions.pageIndex = pageIndex;");

                s.Add($"");
                s.Add($"        const subject = new Subject<{rel.ChildEntity.Name}SearchResponse>()");
                s.Add($"");
                s.Add($"        this.{rel.ChildEntity.Name.ToCamelCase()}Service.search(this.{rel.CollectionName.ToCamelCase()}SearchOptions)");
                s.Add($"            .subscribe(");
                s.Add($"                response => {{");
                s.Add($"                    subject.next(response);");
                s.Add($"                    this.{rel.CollectionName.ToCamelCase()} = response.{rel.ChildEntity.PluralName.ToCamelCase()};");
                s.Add($"                    this.{rel.CollectionName.ToCamelCase()}Headers = response.headers;");
                s.Add($"                }},");
                s.Add($"                err => {{");
                s.Add($"                    this.errorService.handleError(err, \"{rel.CollectionFriendlyName}\", \"Load\");");
                s.Add($"                }}");
                s.Add($"            );");
                s.Add($"");
                s.Add($"        return subject;");
                s.Add($"");
                s.Add($"    }}");
                s.Add($"");
                // todo: use relative links? can then disable 'includeEntities' on these entities
                s.Add($"    goTo{rel.ChildEntity.Name}({rel.ChildEntity.Name.ToCamelCase()}: {rel.ChildEntity.Name}): void {{");
                s.Add($"        this.router.navigate({GetRouterLink(rel.ChildEntity, CurrentEntity)});");
                s.Add($"    }}");
                s.Add($"");
                if (rel.UseMultiSelect)
                {
                    var reverseRel = rel.ChildEntity.RelationshipsAsChild.Where(o => o.RelationshipId != rel.RelationshipId).SingleOrDefault();

                    s.Add($"    add{rel.CollectionName}(): void {{");
                    s.Add($"        this.{reverseRel.ParentEntity.Name.ToCamelCase()}Modal.open();");
                    s.Add($"    }}");
                    s.Add($"");

                    s.Add($"    change{reverseRel.ParentEntity.Name}({reverseRel.ParentEntity.PluralName.ToCamelCase()}: {reverseRel.ParentEntity.Name}[]): void {{");
                    s.Add($"        if (!{reverseRel.ParentEntity.PluralName.ToCamelCase()}.length) return;");
                    s.Add($"        const {reverseRel.RelationshipFields.First().ParentField.Name.ToCamelCase()}List = {reverseRel.ParentEntity.PluralName.ToCamelCase()}.map(o => o.{reverseRel.RelationshipFields.First().ParentField.Name.ToCamelCase()});");
                    s.Add($"        this.{CurrentEntity.Name.ToCamelCase()}Service.save{rel.ChildEntity.PluralName}({CurrentEntity.KeyFields.Select(o => $"this.{CurrentEntity.Name.ToCamelCase()}.{o.Name.ToCamelCase()}").Aggregate((current, next) => { return current + ", " + next; })}, {reverseRel.RelationshipFields.First().ParentField.Name.ToCamelCase()}List)");
                    s.Add($"            .subscribe(");
                    s.Add($"                () => {{");
                    s.Add($"                    this.toastr.success(\"The {rel.ChildEntity.PluralFriendlyName.ToLower()} have been saved\", \"Save {rel.ChildEntity.PluralFriendlyName}\");");
                    if (rel.ChildEntity.HasASortField)
                        s.Add($"                    this.load{rel.CollectionName}();");
                    else
                        s.Add($"                    this.load{rel.CollectionName}(this.{rel.CollectionName.ToCamelCase()}Headers.pageIndex);");
                    s.Add($"                }},");
                    s.Add($"                err => {{");
                    s.Add($"                    this.errorService.handleError(err, \"{rel.ChildEntity.PluralFriendlyName}\", \"Save\");");
                    s.Add($"                }}");
                    s.Add($"            );");
                    s.Add($"    }}");
                    s.Add($"");
                }
                s.Add($"    delete{rel.CollectionName}({rel.ChildEntity.Name.ToCamelCase()}: {rel.ChildEntity.Name}, event: MouseEvent): void {{");
                s.Add($"        event.stopPropagation();");
                s.Add($"");
                s.Add($"        if (!confirm('Are you sure you want to delete this {rel.ChildEntity.FriendlyName.ToLower()}?')) return;");
                s.Add($"");
                s.Add($"        this.{rel.ChildEntity.Name.ToCamelCase()}Service.delete({rel.ChildEntity.KeyFields.Select(o => $"{rel.ChildEntity.Name.ToCamelCase()}.{o.Name.ToCamelCase()}").Aggregate((current, next) => { return current + ", " + next; })})");
                s.Add($"            .subscribe(");
                s.Add($"                () => {{");
                s.Add($"                    this.toastr.success(\"The {rel.ChildEntity.FriendlyName.ToLower()} has been deleted\", \"Delete {rel.ChildEntity.FriendlyName}\");");
                s.Add($"                    this.load{rel.CollectionName}();");
                s.Add($"                }},");
                s.Add($"                err => {{");
                s.Add($"                    this.errorService.handleError(err, \"{rel.ChildEntity.FriendlyName}\", \"Delete\");");
                s.Add($"                }}");
                s.Add($"            );");
                s.Add($"    }}");
                s.Add($"");
                s.Add($"    deleteAll{rel.CollectionName}(): void {{");
                s.Add($"        if (!confirm('Are you sure you want to delete all the {rel.CollectionFriendlyName.ToLower()}?')) return;");
                s.Add($"");
                s.Add($"        this.{CurrentEntity.Name.ToCamelCase()}Service.delete{rel.CollectionName}({CurrentEntity.KeyFields.Select(o => $"this.{CurrentEntity.Name.ToCamelCase()}.{o.Name.ToCamelCase()}").Aggregate((current, next) => { return current + ", " + next; })})");
                s.Add($"            .subscribe(");
                s.Add($"                () => {{");
                s.Add($"                    this.toastr.success(\"The {rel.CollectionFriendlyName.ToLower()} have been deleted\", \"Delete {rel.CollectionFriendlyName}\");");
                s.Add($"                    this.load{rel.CollectionName}();");
                s.Add($"                }},");
                s.Add($"                err => {{");
                s.Add($"                    this.errorService.handleError(err, \"{rel.CollectionFriendlyName}\", \"Delete\");");
                s.Add($"                }}");
                s.Add($"            );");
                s.Add($"    }}");
                s.Add($"");
            }

            foreach (var relationship in relationshipsAsParent.Where(o => o.Hierarchy && o.ChildEntity.HasASortField))
            {
                s.Add($"    sort{relationship.CollectionName}(event: CdkDragDrop<{relationship.ChildEntity.Name}[]>) {{");
                s.Add($"        moveItemInArray(this.{relationship.CollectionName.ToCamelCase()}, event.previousIndex, event.currentIndex);");
                s.Add($"        this.{relationship.ChildEntity.Name.ToCamelCase()}Service.sort(this.{relationship.CollectionName.ToCamelCase()}.map(o => o.{relationship.ChildEntity.KeyFields[0].Name.ToCamelCase()})).subscribe(");
                s.Add($"            () => {{");
                s.Add($"                this.toastr.success(\"The sort order has been updated\", \"Change Sort Order\");");
                s.Add($"            }},");
                s.Add($"            err => {{");
                s.Add($"                this.errorService.handleError(err, \"{relationship.ChildEntity.PluralFriendlyName}\", \"Sort\");");
                s.Add($"            }});");
                s.Add($"    }}");
                s.Add($"");
            }

            s.Add($"}}");

            return RunCodeReplacements(s.ToString(), CodeType.EditTypeScript);

        }

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
                if (CurrentEntity.RelationshipsAsChild.Any(r => r.RelationshipFields.Any(rf => rf.ChildFieldId == field.FieldId) && r.UseSelectorDirective))
                    relationship = CurrentEntity.GetParentSearchRelationship(field);

                if (field.FieldType == FieldType.Enum || relationship != null)
                {
                    if (field.FieldType == FieldType.Enum)
                        filterFields += $" [{field.Name.ToCamelCase()}]=\"{field.Name.ToCamelCase()}\"";
                    else
                        filterFields += $" [{relationship.ParentName.ToCamelCase()}]=\"{relationship.ParentName.ToCamelCase()}\"";
                }
            }
            s.Add(RunTemplateReplacements(file)
                .Replace("/*FILTER-FIELDS*/", filterFields));

            return RunCodeReplacements(s.ToString(), CodeType.AppSelectHtml);
        }

        public string GenerateSelectTypeScript()
        {
            var s = new StringBuilder();

            var file = File.ReadAllText(AppDomain.CurrentDomain.BaseDirectory + "templates/appselect.ts.txt");

            var filterAttributes = string.Empty;
            var filterOptions = string.Empty;
            var inputs = string.Empty;
            var imports = string.Empty;

            if (CurrentEntity.EntityType == EntityType.User)
            {
                imports += $"import {{ Role }} from '../common/models/roles.model';" + Environment.NewLine;
                inputs += $"    @Input() role: Role;" + Environment.NewLine;
            }

            var imported = new List<string>();
            foreach (var field in CurrentEntity.Fields.Where(o => o.SearchType == SearchType.Exact && (o.FieldType == FieldType.Enum || CurrentEntity.RelationshipsAsChild.Any(r => r.RelationshipFields.Any(f => f.ChildFieldId == o.FieldId)))).OrderBy(f => f.FieldOrder))
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
                else if (relationship != null)
                {
                    inputs += $"    @Input() {relationship.ParentName.ToCamelCase()}: {relationship.ParentEntity.Name};" + Environment.NewLine;
                    if (relationship.ParentEntity != CurrentEntity && !imported.Contains(relationship.ParentEntity.Name))
                    {
                        imported.Add(relationship.ParentEntity.Name);
                        imports += $"import {{ {relationship.ParentEntity.Name} }} from '../common/models/{relationship.ParentEntity.Name.ToLower()}.model';" + Environment.NewLine;
                    }
                }
            }
            if (CurrentEntity.PrimaryField.CustomType == CustomType.Date)
                imports += $"import * as moment from 'moment';" + Environment.NewLine;

            if (CurrentEntity.PrimaryField == null) throw new Exception("Entity " + CurrentEntity.Name + " does not have a Primary Field defined for AppSelect label");

            var LABEL_OUTPUT_MULTI = $"{CurrentEntity.CamelCaseName}.{CurrentEntity.PrimaryField.Name.ToCamelCase()}";
            var LABEL_OUTPUT_SINGLE = $"this.{CurrentEntity.CamelCaseName}?.{CurrentEntity.PrimaryField.Name.ToCamelCase()}";

            if (CurrentEntity.PrimaryField.CustomType == CustomType.Date)
            {
                LABEL_OUTPUT_MULTI = $"moment({LABEL_OUTPUT_MULTI}).format(\"LL\")";
                LABEL_OUTPUT_SINGLE = $"moment({LABEL_OUTPUT_SINGLE}).format(\"LL\")";
            }
            else if (CurrentEntity.PrimaryField.FieldType == FieldType.Enum)
            {
                LABEL_OUTPUT_MULTI = $"Enums.{CurrentEntity.PrimaryField.Lookup.PluralName}[{LABEL_OUTPUT_MULTI}]?.label";
                LABEL_OUTPUT_SINGLE = $"Enums.{CurrentEntity.PrimaryField.Lookup.PluralName}[{LABEL_OUTPUT_SINGLE}]?.label";
            }

            var enums = CurrentEntity.PrimaryField.FieldType == FieldType.Enum ? ", Enums" : string.Empty;

            file = RunTemplateReplacements(file)
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

        public string GenerateModalHtml()
        {
            var s = new StringBuilder();

            var file = File.ReadAllText(AppDomain.CurrentDomain.BaseDirectory + "templates/selectmodal.html");

            var fieldHeaders = string.Empty;
            var fieldList = string.Empty;
            var appSelectFilters = string.Empty;
            var filterAlerts = string.Empty;
            var appTextFilter = string.Empty;

            foreach (var field in CurrentEntity.Fields.Where(f => f.ShowInSearchResults).OrderBy(f => f.FieldOrder))
            {
                var ngIf = string.Empty;
                if (CurrentEntity.Fields.Any(o => o.FieldId == field.FieldId && o.SearchType == SearchType.Exact))
                {
                    if (field.FieldType == FieldType.Enum)
                        ngIf = " *ngIf=\"!" + field.Name.ToCamelCase() + $"\"";
                    else
                    {
                        if (CurrentEntity.RelationshipsAsChild.Any(r => r.RelationshipFields.Any(rf => rf.ChildFieldId == field.FieldId) && r.UseSelectorDirective))
                        {
                            var relationship = CurrentEntity.GetParentSearchRelationship(field);
                            ngIf = " *ngIf=\"!" + relationship.ParentName.ToCamelCase().ToCamelCase() + $"\"";
                        }
                    }
                }

                fieldHeaders += (fieldHeaders == string.Empty ? string.Empty : Environment.NewLine) + $"                    <th{ngIf}>{field.Label}</th>";
                fieldList += (fieldList == string.Empty ? string.Empty : Environment.NewLine);

                fieldList += $"                    <td{ngIf}>{field.ListFieldHtml}</td>";
            }

            if (CurrentEntity.Fields.Any(o => o.SearchType == SearchType.Text))
            {
                appTextFilter += $"                    <div class=\"col-sm-6 col-md-6 col-lg-4\">" + Environment.NewLine;
                appTextFilter += $"                        <div class=\"form-group\">" + Environment.NewLine;
                appTextFilter += $"                            <input type=\"search\" ngbAutofocus name=\"q\" id=\"q\" [(ngModel)]=\"searchOptions.q\" max=\"100\" class=\"form-control\" placeholder=\"Search PLURALFRIENDLYNAME_TOLOWER\" />" + Environment.NewLine;
                appTextFilter += $"                        </div>" + Environment.NewLine;
                appTextFilter += $"                    </div>" + Environment.NewLine;
                appTextFilter += Environment.NewLine;
            }

            if (CurrentEntity.EntityType == EntityType.User)
                filterAlerts += Environment.NewLine + $"                <div class=\"alert alert-info alert-dismissible\" *ngIf=\"role!=undefined\"><button type=\"button\" class=\"close\" data-dismiss=\"alert\" aria-label=\"Close\" (click)=\"searchOptions.roleName=undefined;role=undefined;runSearch();\"><span aria-hidden=\"true\" *ngIf=\"canRemoveFilters\">&times;</span></button>Filtered by role: {{{{role.label}}}}</div>" + Environment.NewLine;

            foreach (var field in CurrentEntity.Fields.Where(f => f.SearchType == SearchType.Exact).OrderBy(f => f.FieldOrder))
            {
                Relationship relationship = null;
                if (CurrentEntity.RelationshipsAsChild.Any(r => r.RelationshipFields.Any(rf => rf.ChildFieldId == field.FieldId) && r.UseSelectorDirective))
                    relationship = CurrentEntity.GetParentSearchRelationship(field);

                if (field.FieldType == FieldType.Enum || relationship != null)
                {
                    if (field.FieldType == FieldType.Enum)
                    {
                        appSelectFilters += $"                    <div class=\"col-sm-6 col-md-6 col-lg-4\" *ngIf=\"!{field.Name.ToCamelCase()}\">" + Environment.NewLine;
                        appSelectFilters += $"                        <select id=\"{field.Name.ToCamelCase()}\" name=\"{field.Name.ToCamelCase()}\" [(ngModel)]=\"searchOptions.{field.Name.ToCamelCase()}\" #{field.Name.ToCamelCase()}=\"ngModel\" class=\"form-control\">" + Environment.NewLine;
                        appSelectFilters += $"                            <option *ngFor=\"let {field.Lookup.Name.ToCamelCase()} of {field.Lookup.PluralName.ToCamelCase()}\" [ngValue]=\"{field.Lookup.Name.ToCamelCase()}.value\">{{{{ {field.Lookup.Name.ToCamelCase()}.label }}}}</option>" + Environment.NewLine;
                        appSelectFilters += $"                        </select>" + Environment.NewLine;
                        appSelectFilters += $"                    </div>" + Environment.NewLine;
                    }
                    else
                    {
                        appSelectFilters += $"                    <div class=\"col-sm-6 col-md-6 col-lg-4\" *ngIf=\"!{relationship.ParentName.ToCamelCase()}\">" + Environment.NewLine;
                        appSelectFilters += $"                        <div class=\"form-group\">" + Environment.NewLine;
                        appSelectFilters += $"                            {relationship.AppSelector}" + Environment.NewLine;
                        appSelectFilters += $"                        </div>" + Environment.NewLine;
                        appSelectFilters += $"                    </div>" + Environment.NewLine;
                    }
                    appSelectFilters += Environment.NewLine;

                    if (filterAlerts == string.Empty) filterAlerts = Environment.NewLine;

                    if (field.FieldType == FieldType.Enum)
                        filterAlerts += $"                <div class=\"alert alert-info alert-dismissible\" *ngIf=\"{field.Name.ToCamelCase()}!=undefined\"><button type=\"button\" class=\"close\" data-dismiss=\"alert\" aria-label=\"Close\" (click)=\"searchOptions.{field.Name.ToCamelCase()}=undefined;{field.Name.ToCamelCase()}=undefined;runSearch();\"><span aria-hidden=\"true\" *ngIf=\"canRemoveFilters\">&times;</span></button>Filtered by {field.Label.ToLower()}: {{{{{field.Name.ToCamelCase()}.label}}}}</div>" + Environment.NewLine;
                    else
                        filterAlerts += $"                <div class=\"alert alert-info alert-dismissible\" *ngIf=\"{relationship.ParentName.ToCamelCase()}!=undefined\"><button type=\"button\" class=\"close\" data-dismiss=\"alert\" aria-label=\"Close\" (click)=\"searchOptions.{field.Name.ToCamelCase()}=undefined;{relationship.ParentName.ToCamelCase()}=undefined;runSearch();\"><span aria-hidden=\"true\" *ngIf=\"canRemoveFilters\">&times;</span></button>Filtered by {field.Label.ToLower()}: {{{{{relationship.ParentName.ToCamelCase()}.{relationship.ParentField.Name.ToCamelCase()}}}}}</div>" + Environment.NewLine;
                }
            }

            file = RunTemplateReplacements(file.Replace("APP_TEXT_FILTER", appTextFilter))
                .Replace("FIELD_HEADERS", fieldHeaders)
                .Replace("FIELD_LIST", fieldList)
                .Replace("APP_SELECT_FILTERS", appSelectFilters)
                .Replace("FILTER_ALERTS", filterAlerts);

            s.Add(file);

            return RunCodeReplacements(s.ToString(), CodeType.SelectModalHtml);
        }

        public string GenerateModalTypeScript()
        {
            var s = new StringBuilder();

            var file = File.ReadAllText(AppDomain.CurrentDomain.BaseDirectory + "templates/selectmodal.ts.txt");

            var filterParams = string.Empty;
            var inputs = string.Empty;
            var imports = string.Empty;
            var properties = string.Empty;
            var searchOptions = string.Empty;

            if (CurrentEntity.EntityType == EntityType.User)
            {
                imports += $"import {{ Role }} from '../common/models/roles.model';" + Environment.NewLine;
                inputs += $"    @Input() role: Role;" + Environment.NewLine;
                searchOptions += $"        this.searchOptions.roleName = this.role ? this.role.name : undefined;" + Environment.NewLine;
            }

            var lookups = CurrentEntity.Fields.Where(f => f.SearchType == SearchType.Exact && f.FieldType == FieldType.Enum).Select(f => f.Lookup).Distinct().OrderBy(o => o.Name);
            if (lookups.Any())
                imports += $"import {{ Enum, Enums }} from '../common/models/enums.model';" + Environment.NewLine;

            if (!lookups.Any() && CurrentEntity.Fields.Where(o => o.ShowInSearchResults && o.FieldType == FieldType.Enum).Any())
            {
                imports += $"import {{ Enums }} from '../common/models/enums.model';" + Environment.NewLine;
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
                        imports += $"import {{ {relationship.ParentEntity.Name} }} from '../common/models/{relationship.ParentEntity.Name.ToLower()}.model';" + Environment.NewLine;
                    }
                }
            }

            file = RunTemplateReplacements(file)
                .Replace("/*IMPORTS*/", imports)
                .Replace("/*INPUTS*/", inputs)
                .Replace("/*PROPERTIES*/", properties)
                .Replace("/*SEARCHOPTIONS*/", searchOptions)
                .Replace("/*FILTER_PARAMS*/", filterParams);
            //.Replace("/*FILTER_TRIGGERS*/", filterTriggers);

            s.Add(file);

            return RunCodeReplacements(s.ToString(), CodeType.SelectModalTypeScript);
        }

        private string RunTemplateReplacements(string input)
        {
            if (CurrentEntity.KeyFields.Count > 1 && input.Contains("KEYFIELD"))
            {
                // input type=hidden uses KEYFIELD, so app selects can't use where they would return more than 1 key field
                throw new Exception("Unable to run key field replacements (multiple keys). Disable app-selects if not required? " + CurrentEntity.Name);
            }

            return input
                .Replace("PLURALNAME_TOCAMELCASE", CurrentEntity.PluralName.ToCamelCase())
                .Replace("CAMELCASENAME", CurrentEntity.CamelCaseName)
                .Replace("PLURALFRIENDLYNAME_TOLOWER", CurrentEntity.PluralFriendlyName.ToLower())
                .Replace("PLURALFRIENDLYNAME", CurrentEntity.PluralFriendlyName)
                .Replace("FRIENDLYNAME_LOWER", CurrentEntity.FriendlyName.ToLower())
                .Replace("FRIENDLYNAME", CurrentEntity.FriendlyName)
                .Replace("PLURALNAME", CurrentEntity.PluralName)
                .Replace("NAME_TOLOWER", CurrentEntity.Name.ToLower())
                .Replace("HYPHENATEDNAME", CurrentEntity.Name.Hyphenated())
                .Replace("KEYFIELDTYPE", CurrentEntity.KeyFields.First().JavascriptType)
                .Replace("KEYFIELD", CurrentEntity.KeyFields.First().Name.ToCamelCase())
                .Replace("NAME", CurrentEntity.Name)
                .Replace("STARTSWITHVOWEL", new Regex("^[aeiou]").IsMatch(CurrentEntity.Name.ToLower()) ? "n" : "")
                .Replace("ICONLINK", GetIconLink(CurrentEntity))
                .Replace("ADDNEWURL", "/" + CurrentEntity.PluralName.ToLower() + "/add")
                .Replace("// <reference", "/// <reference");
        }

        private string GetIconLink(Entity entity)
        {
            if (String.IsNullOrWhiteSpace(entity.IconClass)) return string.Empty;

            string html = $@"<span class=""input-group-prepend"" *ngIf=""!multiple && !!{entity.Name.ToCamelCase()}"">
        <a routerLink=""{GetHierarchyString(entity)}"" class=""btn btn-secondary"" [ngClass]=""{{ 'disabled': disabled }}"">
            <i class=""fas {entity.IconClass}""></i>
        </a>
    </span>
    ";
            return html;
        }

        // ---- HELPER METHODS -----------------------------------------------------------------------

        private string GetHierarchyString(Entity entity, string prefix = null)
        {
            var hierarchyRelationship = entity.RelationshipsAsChild.SingleOrDefault(o => o.Hierarchy);
            var parents = "";
            if (hierarchyRelationship != null)
            {
                parents = GetHierarchyString(hierarchyRelationship.ParentEntity, (prefix == null ? "" : prefix + ".") + entity.Name.ToCamelCase());
            }
            return parents + "/" + entity.PluralName.ToLower() + "/{{$any(" + (prefix == null ? "" : prefix + ".") + entity.Name.ToCamelCase() + ")." + entity.KeyFields.Single().Name.ToCamelCase() + "}}";
        }

        private string ClimbHierarchy(Relationship relationship, string result)
        {
            result += "." + relationship.ParentName;
            foreach (var relAbove in relationship.ParentEntity.RelationshipsAsChild.Where(r => r.Hierarchy))
                result = ClimbHierarchy(relAbove, result);
            return result;
        }

        private string RunCodeReplacements(string code, CodeType type)
        {

            if (!String.IsNullOrWhiteSpace(CurrentEntity.PreventApiResourceDeployment) && type == CodeType.ApiResource) return code;
            if (!String.IsNullOrWhiteSpace(CurrentEntity.PreventAppRouterDeployment) && type == CodeType.AppRouter) return code;
            if (!String.IsNullOrWhiteSpace(CurrentEntity.PreventBundleConfigDeployment) && type == CodeType.BundleConfig) return code;
            if (!String.IsNullOrWhiteSpace(CurrentEntity.PreventControllerDeployment) && type == CodeType.Controller) return code;
            if (!String.IsNullOrWhiteSpace(CurrentEntity.PreventDbContextDeployment) && type == CodeType.DbContext) return code;
            if (!String.IsNullOrWhiteSpace(CurrentEntity.PreventDTODeployment) && type == CodeType.DTO) return code;
            if (!String.IsNullOrWhiteSpace(CurrentEntity.PreventEditHtmlDeployment) && type == CodeType.EditHtml) return code;
            if (!String.IsNullOrWhiteSpace(CurrentEntity.PreventEditTypeScriptDeployment) && type == CodeType.EditTypeScript) return code;
            if (!String.IsNullOrWhiteSpace(CurrentEntity.PreventListHtmlDeployment) && type == CodeType.ListHtml) return code;
            if (!String.IsNullOrWhiteSpace(CurrentEntity.PreventListTypeScriptDeployment) && type == CodeType.ListTypeScript) return code;
            if (!String.IsNullOrWhiteSpace(CurrentEntity.PreventModelDeployment) && type == CodeType.Model) return code;
            if (!String.IsNullOrWhiteSpace(CurrentEntity.PreventAppSelectHtmlDeployment) && type == CodeType.AppSelectHtml) return code;
            if (!String.IsNullOrWhiteSpace(CurrentEntity.PreventAppSelectTypeScriptDeployment) && type == CodeType.AppSelectTypeScript) return code;
            if (!String.IsNullOrWhiteSpace(CurrentEntity.PreventSelectModalHtmlDeployment) && type == CodeType.SelectModalHtml) return code;
            if (!String.IsNullOrWhiteSpace(CurrentEntity.PreventSelectModalTypeScriptDeployment) && type == CodeType.SelectModalTypeScript) return code;

            // todo: needs a sort order

            var replacements = CurrentEntity.CodeReplacements.Where(cr => !cr.Disabled && cr.CodeType == type).ToList();
            replacements.InsertRange(0, DbContext.CodeReplacements.Where(o => o.Entity.ProjectId == CurrentEntity.ProjectId && !o.Disabled && o.CodeType == CodeType.Global).ToList());

            // common scripts need a common replacement
            if (type == CodeType.Enums || type == CodeType.AppRouter || type == CodeType.BundleConfig || type == CodeType.DbContext || type == CodeType.SharedModule)
                replacements = CodeReplacements.Where(cr => !cr.Disabled && cr.CodeType == type && cr.Entity.ProjectId == CurrentEntity.ProjectId).ToList();

            foreach (var replacement in replacements.OrderBy(o => o.SortOrder))
            {
                var findCode = replacement.FindCode.Replace("\\", @"\\").Replace("(", "\\(").Replace(")", "\\)").Replace("[", "\\[").Replace("]", "\\]").Replace("?", "\\?").Replace("*", "\\*").Replace("$", "\\$").Replace("+", "\\+").Replace("{", "\\{").Replace("}", "\\}").Replace("|", "\\|").Replace("^", "\\^").Replace("\n", "\r\n").Replace("\r\r", "\r");
                var re = new Regex(findCode);
                if (replacement.CodeType != CodeType.Global && !re.IsMatch(code))
                    throw new Exception($"Code Replacement failed on {replacement.Entity.Name}.{type}: {replacement.Purpose}");
                code = re.Replace(code, replacement.ReplacementCode ?? string.Empty).Replace("\n", "\r\n").Replace("\r\r", "\r");
            }
            return code;
        }

        private string GetKeyFieldLinq(string entityName, string otherEntityName = null, string comparison = "==", string joiner = "&&", bool addParenthesisIfMultiple = false)
        {
            return (addParenthesisIfMultiple && CurrentEntity.KeyFields.Count() > 1 ? "(" : "") +
                CurrentEntity.KeyFields
                    .Select(o => $"{entityName}.{o.Name} {comparison} " + (otherEntityName == null ? o.Name.ToCamelCase() : $"{otherEntityName}.{o.Name}")).Aggregate((current, next) => $"{current} {joiner} {next}")
                    + (addParenthesisIfMultiple && CurrentEntity.KeyFields.Count() > 1 ? ")" : "")
                    ;
        }

        public List<string> GetTopAncestors(List<string> list, string prefix, Relationship relationship, RelationshipAncestorLimits ancestorLimit, int level = 0, bool includeIfHierarchy = false)
        {
            // override is for the controller.get, when in a hierarchy. need to return the full hierarchy, so that the breadcrumb will be set correctly
            // change: commented out ancestorLimit as (eg) KTUPACK Recommendation-Topic had IncludeAllParents but was therefore setting overrideLimit to false
            var overrideLimit = relationship.Hierarchy && includeIfHierarchy && ancestorLimit != RelationshipAncestorLimits.IncludeAllParents;

            //if (relationship.RelationshipAncestorLimit == RelationshipAncestorLimits.Exclude) return list;
            prefix += "." + relationship.ParentName;
            if (!overrideLimit && ancestorLimit == RelationshipAncestorLimits.IncludeRelatedEntity && level == 0)
            {
                list.Add(prefix);
            }
            else if (!overrideLimit && ancestorLimit == RelationshipAncestorLimits.IncludeRelatedParents && level == 1)
            {
                list.Add(prefix);
            }
            else if (includeIfHierarchy && relationship.Hierarchy)
            {
                var hierarchyRel = relationship.ParentEntity.RelationshipsAsChild.SingleOrDefault(o => o.Hierarchy);
                if (hierarchyRel != null)
                {
                    list = GetTopAncestors(list, prefix, hierarchyRel, ancestorLimit, level + 1, includeIfHierarchy);
                }
                else if (includeIfHierarchy)
                {
                    list.Add(prefix);
                }
            }
            else if (relationship.ParentEntity.RelationshipsAsChild.Any() && relationship.ParentEntityId != relationship.ChildEntityId)
            {
                foreach (var parentRelationship in relationship.ParentEntity.RelationshipsAsChild.Where(r => r.RelationshipAncestorLimit != RelationshipAncestorLimits.Exclude))
                {
                    // if building the hierarchy links, only continue adding if it's still in the hierarchy
                    if (includeIfHierarchy && !parentRelationship.Hierarchy) continue;

                    // if got here because not overrideLimit, otherwise if it IS, then only if the parent relationship is the hierarchy
                    if (overrideLimit || parentRelationship.Hierarchy)
                        list = GetTopAncestors(list, prefix, parentRelationship, ancestorLimit, level + 1, includeIfHierarchy);
                }
                //if (list.Count == 0 && includeIfHierarchy) list.Add();
            }
            else
            {
                list.Add(prefix);
            }
            return list;
        }

        public string Validate()
        {
            if (CurrentEntity.Fields.Count == 0) return "No fields are defined";
            if (CurrentEntity.KeyFields.Count == 0) return "No key fields are defined";
            if (!CurrentEntity.Fields.Any(f => f.ShowInSearchResults)) return "No fields are designated as search result fields";
            var rel = CurrentEntity.RelationshipsAsChild.FirstOrDefault(r => r.RelationshipFields.Count == 0);
            if (rel != null) return $"Relationship {rel.CollectionName} (to {rel.ParentEntity.FriendlyName}) has no link fields defined";
            rel = CurrentEntity.RelationshipsAsParent.FirstOrDefault(r => r.RelationshipFields.Count == 0);
            if (rel != null) return $"Relationship {rel.CollectionName} (to {rel.ChildEntity.FriendlyName}) has no link fields defined";
            //if (CurrentEntity.RelationshipsAsChild.Where(r => r.Hierarchy).Count() > 1) return $"{CurrentEntity.Name} is a hierarchical child on more than one relationship";
            if (CurrentEntity.RelationshipsAsParent.Any(r => r.UseMultiSelect && !r.DisplayListOnParent)) return "Using Multi-Select requires that the relationship is also displayed on the parent";
            if (CurrentEntity.RelationshipsAsParent.Any(r => r.UseMultiSelect && !r.DisplayListOnParent)) return "Using Multi-Select requires that the relationship is also displayed on the parent";
            if (CurrentEntity.Fields.Any(o => o.IsUniqueOnHierarchy) && !CurrentEntity.RelationshipsAsChild.Any(o => o.Hierarchy))
                return "IsUniqueOnHierarchy requires a hierarchical relationship";

            return null;
        }

        public static string RunDeployment(ApplicationDbContext DbContext, Entity entity, DeploymentOptions deploymentOptions)
        {
            if (entity.PrimaryFieldId == null) throw new Exception($"Entity {entity.Name} does not have a Primary Field defined");

            var codeGenerator = new Code(entity, DbContext);

            var error = codeGenerator.Validate();
            if (error != null)
                return (error);

            if (!Directory.Exists(entity.Project.RootPathWeb))
                return ("Web path does not exist: " + entity.Project.RootPathWeb);

            if (deploymentOptions.Model && !string.IsNullOrWhiteSpace(entity.PreventModelDeployment))
                return ("Model deployment is not allowed: " + entity.PreventModelDeployment);
            if (deploymentOptions.DTO && !string.IsNullOrWhiteSpace(entity.PreventDTODeployment))
                return ("DTO deployment is not allowed: " + entity.PreventDTODeployment);
            if (deploymentOptions.DbContext && !string.IsNullOrWhiteSpace(entity.PreventDbContextDeployment))
                return ("DbContext deployment is not allowed: " + entity.PreventDbContextDeployment);
            if (deploymentOptions.Controller && !string.IsNullOrWhiteSpace(entity.PreventControllerDeployment))
                return ("Controller deployment is not allowed: " + entity.PreventControllerDeployment);
            if (deploymentOptions.BundleConfig && !string.IsNullOrWhiteSpace(entity.PreventBundleConfigDeployment))
                return ("BundleConfig deployment is not allowed: " + entity.PreventBundleConfigDeployment);
            if (deploymentOptions.AppRouter && !string.IsNullOrWhiteSpace(entity.PreventAppRouterDeployment))
                return ("AppRouter deployment is not allowed: " + entity.PreventAppRouterDeployment);
            if (deploymentOptions.ApiResource && !string.IsNullOrWhiteSpace(entity.PreventApiResourceDeployment))
                return ("ApiResource deployment is not allowed: " + entity.PreventApiResourceDeployment);
            if (deploymentOptions.ListHtml && !string.IsNullOrWhiteSpace(entity.PreventListHtmlDeployment))
                return ("ListHtml deployment is not allowed: " + entity.PreventListHtmlDeployment);
            if (deploymentOptions.ListTypeScript && !string.IsNullOrWhiteSpace(entity.PreventListTypeScriptDeployment))
                return ("ListTypeScript deployment is not allowed: " + entity.PreventListTypeScriptDeployment);
            if (deploymentOptions.EditHtml && !string.IsNullOrWhiteSpace(entity.PreventEditHtmlDeployment))
                return ("EditHtml deployment is not allowed: " + entity.PreventEditHtmlDeployment);
            if (deploymentOptions.EditTypeScript && !string.IsNullOrWhiteSpace(entity.PreventEditTypeScriptDeployment))
                return ("EditTypeScript deployment is not allowed: " + entity.PreventEditTypeScriptDeployment);
            if (deploymentOptions.AppSelectHtml && !string.IsNullOrWhiteSpace(entity.PreventAppSelectHtmlDeployment))
                return ("AppSelectHtml deployment is not allowed: " + entity.PreventAppSelectHtmlDeployment);
            if (deploymentOptions.AppSelectTypeScript && !string.IsNullOrWhiteSpace(entity.PreventAppSelectTypeScriptDeployment))
                return ("AppSelectTypeScript deployment is not allowed: " + entity.PreventAppSelectTypeScriptDeployment);
            if (deploymentOptions.SelectModalHtml && !string.IsNullOrWhiteSpace(entity.PreventSelectModalHtmlDeployment))
                return ("SelectModalHtml deployment is not allowed: " + entity.PreventSelectModalHtmlDeployment);
            if (deploymentOptions.SelectModalTypeScript && !string.IsNullOrWhiteSpace(entity.PreventSelectModalTypeScriptDeployment))
                return ("SelectModalTypeScript deployment is not allowed: " + entity.PreventSelectModalTypeScriptDeployment);

            if (deploymentOptions.DbContext)
            {
                var firstEntity = DbContext.Entities.SingleOrDefault(e => !e.Exclude && e.ProjectId == entity.ProjectId && e.PreventDbContextDeployment.Length > 0);
                if (firstEntity != null)
                    return ("DbContext deployment is not allowed on " + firstEntity.Name + ": " + firstEntity.PreventDbContextDeployment);
            }
            if (deploymentOptions.BundleConfig)
            {
                var firstEntity = DbContext.Entities.SingleOrDefault(e => !e.Exclude && e.ProjectId == entity.ProjectId && e.PreventBundleConfigDeployment.Length > 0);
                if (firstEntity != null)
                    return ("BundleConfig deployment is not allowed on " + firstEntity.Name + ": " + firstEntity.PreventBundleConfigDeployment);
            }
            if (deploymentOptions.AppRouter)
            {
                var firstEntity = DbContext.Entities.SingleOrDefault(e => !e.Exclude && e.ProjectId == entity.ProjectId && e.PreventAppRouterDeployment.Length > 0);
                if (firstEntity != null)
                    return ("AppRouter deployment is not allowed on " + firstEntity.Name + ": " + firstEntity.PreventAppRouterDeployment);
            }
            //if (deploymentOptions.ApiResource)
            //{
            //    var firstEntity = DbContext.Entities.SingleOrDefault(e => !e.Exclude && e.ProjectId == entity.ProjectId && e.PreventApiResourceDeployment.Length > 0);
            //    if (firstEntity != null)
            //        return ("ApiResource deployment is not allowed on " + firstEntity.Name + ": " + firstEntity.PreventApiResourceDeployment);
            //}

            #region model
            if (deploymentOptions.Model)
            {
                var path = Path.Combine(entity.Project.RootPathModels, "Models");
                if (!Directory.Exists(path))
                    return ("Models path does not exist: " + path);

                var code = codeGenerator.GenerateModel();
                if (code != string.Empty) File.WriteAllText(Path.Combine(path, entity.Name + ".cs"), code);
            }
            #endregion

            #region typescript model
            if (deploymentOptions.TypeScriptModel)
            {
                var path = Path.Combine(entity.Project.RootPathModels, "Models");
                if (!Directory.Exists(path))
                    return ("Models path does not exist:" + path);

                if (!CreateAppDirectory(entity.Project, "common\\models", codeGenerator.GenerateTypeScriptModel(), entity.Name.ToLower() + ".model.ts"))
                    return ("Failed to create the typescript model folder. Check that the app path does not exists: " + Path.Combine(entity.Project.RootPathWeb, @"ClientApp\src\app"));
            }
            #endregion

            #region dbcontext
            if (deploymentOptions.DbContext)
            {
                var path = Path.Combine(entity.Project.RootPathModels, "Models");
                if (!Directory.Exists(path))
                    return ("Models path does not exist: " + path);

                var code = codeGenerator.GenerateDbContext();
                if (code != string.Empty) File.WriteAllText(Path.Combine(path, "ApplicationDBContext_.cs"), code);
            }
            #endregion

            #region dto
            if (deploymentOptions.DTO)
            {
                var path = Path.Combine(entity.Project.RootPathModels, "Models\\DTOs");
                if (!Directory.Exists(path))
                    return ("DTOs path does not exist: " + path);

                var code = codeGenerator.GenerateDTO();
                if (code != string.Empty) File.WriteAllText(Path.Combine(path, entity.Name + "DTO.cs"), code);
            }
            #endregion

            #region enums
            if (deploymentOptions.Enums)
            {
                var path = Path.Combine(entity.Project.RootPathModels, "Models");
                if (!Directory.Exists(path))
                    return ("Models path does not exist: " + path);

                var code = codeGenerator.GenerateEnums();
                if (code != string.Empty) File.WriteAllText(Path.Combine(path, "Enums.cs"), code);

                // todo: move this to own deployment option
                if (!CreateAppDirectory(entity.Project, "common\\models", codeGenerator.GenerateTypeScriptEnums(), "enums.model.ts"))
                    return ("App path does not exist: " + path);

                // todo: move this to own deployment option
                if (!CreateAppDirectory(entity.Project, "common\\models", codeGenerator.GenerateTypeScriptRoles(), "roles.model.ts"))
                    return ("App path does not exist: " + path);

                // todo: move this to own deployment option
                File.WriteAllText(Path.Combine(entity.Project.RootPathModels, "Models", "Roles.cs"), codeGenerator.GenerateRoles());

            }
            #endregion

            #region settings
            if (deploymentOptions.SettingsDTO)
            {
                var path = Path.Combine(entity.Project.RootPathModels, "Models\\DTOs");
                if (!Directory.Exists(path))
                    return ("DTOs path does not exist: " + path);

                // settings now manually done
                //var code = codeGenerator.GenerateSettingsDTO();
                //if (code != string.Empty) File.WriteAllText(Path.Combine(path, "SettingsDTO_.cs"), code);
            }
            #endregion

            #region settings dto
            if (deploymentOptions.SettingsDTO)
            {
                var path = Path.Combine(entity.Project.RootPathModels, "Models\\DTOs");
                if (!Directory.Exists(path))
                    return ("DTOs path does not exist: " + path);

                //var code = codeGenerator.GenerateSettingsDTO();
                //if (code != string.Empty) File.WriteAllText(Path.Combine(path, "SettingsDTO_.cs"), code);
            }
            #endregion

            #region controller
            if (deploymentOptions.Controller)
            {
                var path = Path.Combine(entity.Project.RootPathWeb, "Controllers");
                if (!Directory.Exists(path))
                    return ("Controllers path does not exist: " + path);

                var code = codeGenerator.GenerateController();
                if (code != string.Empty) File.WriteAllText(Path.Combine(path, entity.PluralName + "Controller.cs"), code);
            }
            #endregion

            #region list html
            if (deploymentOptions.ListHtml)
            {
                if (!CreateAppDirectory(entity.Project, entity.PluralName, codeGenerator.GenerateListHtml(), entity.Name.ToLower() + ".list.component.html"))
                    return ("App path does not exist: " + Path.Combine(entity.Project.RootPathWeb, @"ClientApp\src\app"));
            }
            #endregion

            #region list typescript
            if (deploymentOptions.ListTypeScript)
            {
                if (!CreateAppDirectory(entity.Project, entity.PluralName, codeGenerator.GenerateListTypeScript(), entity.Name.ToLower() + ".list.component.ts"))
                    return ("App path does not exist: " + Path.Combine(entity.Project.RootPathWeb, @"ClientApp\src\app"));
            }
            #endregion

            #region edit html
            if (deploymentOptions.EditHtml)
            {
                if (!CreateAppDirectory(entity.Project, entity.PluralName, codeGenerator.GenerateEditHtml(), entity.Name.ToLower() + ".edit.component.html"))
                    return ("App path does not exist: " + Path.Combine(entity.Project.RootPathWeb, @"ClientApp\src\app"));
            }
            #endregion

            #region edit typescript
            if (deploymentOptions.EditTypeScript)
            {
                if (!CreateAppDirectory(entity.Project, entity.PluralName, codeGenerator.GenerateEditTypeScript(), entity.Name.ToLower() + ".edit.component.ts"))
                    return ("App path does not exist: " + Path.Combine(entity.Project.RootPathWeb, @"ClientApp\src\app"));
            }
            #endregion

            #region api resource
            if (deploymentOptions.ApiResource)
            {
                if (!CreateAppDirectory(entity.Project, "common\\services", codeGenerator.GenerateApiResource(), entity.Name.ToLower() + ".service.ts"))
                    return ("App path does not exist: " + Path.Combine(entity.Project.RootPathWeb, @"ClientApp\src\app"));
            }
            #endregion

            // todo: rename
            #region bundleconfig
            if (deploymentOptions.BundleConfig)
            {
                var path = entity.Project.RootPathWeb + @"ClientApp\src\app\";

                var code = codeGenerator.GenerateBundleConfig();
                if (code != string.Empty) File.WriteAllText(Path.Combine(path, "generated.module.ts"), code);

                code = codeGenerator.GenerateSharedModule();
                if (code != string.Empty) File.WriteAllText(Path.Combine(path, "shared.module.ts"), code);
            }
            #endregion

            #region app router
            if (deploymentOptions.AppRouter)
            {
                var path = entity.Project.RootPathWeb + @"ClientApp\src\app\";

                var code = codeGenerator.GenerateAppRouter();
                if (code != string.Empty) File.WriteAllText(Path.Combine(path, "generated.routes.ts"), code);
            }
            #endregion

            #region app-select html
            if (deploymentOptions.AppSelectHtml)
            {
                if (!CreateAppDirectory(entity.Project, entity.PluralName, codeGenerator.GenerateSelectHtml(), entity.Name.ToLower() + ".select.component.html"))
                    return ("App path does not exist: " + Path.Combine(entity.Project.RootPathWeb, @"ClientApp\src\app"));
            }
            #endregion

            #region app-select typescript
            if (deploymentOptions.AppSelectTypeScript)
            {
                if (!CreateAppDirectory(entity.Project, entity.PluralName, codeGenerator.GenerateSelectTypeScript(), entity.Name.ToLower() + ".select.component.ts"))
                    return ("App path does not exist: " + Path.Combine(entity.Project.RootPathWeb, @"ClientApp\src\app"));
            }
            #endregion

            #region select modal html
            if (deploymentOptions.SelectModalHtml)
            {
                if (!CreateAppDirectory(entity.Project, entity.PluralName, codeGenerator.GenerateModalHtml(), entity.Name.ToLower() + ".modal.component.html"))
                    return ("App path does not exist: " + Path.Combine(entity.Project.RootPathWeb, @"ClientApp\src\app"));
            }
            #endregion

            #region select modal typescript
            if (deploymentOptions.SelectModalTypeScript)
            {
                if (!CreateAppDirectory(entity.Project, entity.PluralName, codeGenerator.GenerateModalTypeScript(), entity.Name.ToLower() + ".modal.component.ts"))
                    return ("App path does not exist: " + Path.Combine(entity.Project.RootPathWeb, @"ClientApp\src\app"));
            }
            #endregion

            return null;
        }

        private static bool CreateAppDirectory(Project project, string directoryName, string code, string fileName)
        {
            if (code == string.Empty) return true;

            var path = Path.Combine(project.RootPathWeb, @"ClientApp\src\app");
            if (!Directory.Exists(path))
                return false;
            path = Path.Combine(path, directoryName.ToLower());
            if (!Directory.Exists(path))
                Directory.CreateDirectory(path);

            File.WriteAllText(Path.Combine(path, fileName), code);
            return true;
        }
    }

    public class DeploymentOptions
    {
        public bool Model { get; set; }
        public bool TypeScriptModel { get; set; }
        public bool Enums { get; set; }
        public bool DTO { get; set; }
        public bool SettingsDTO { get; set; }
        public bool DbContext { get; set; }
        public bool Controller { get; set; }
        public bool BundleConfig { get; set; }
        public bool AppRouter { get; set; }
        public bool ApiResource { get; set; }
        public bool ListHtml { get; set; }
        public bool ListTypeScript { get; set; }
        public bool EditHtml { get; set; }
        public bool EditTypeScript { get; set; }
        public bool AppSelectHtml { get; set; }
        public bool AppSelectTypeScript { get; set; }
        public bool SelectModalHtml { get; set; }
        public bool SelectModalTypeScript { get; set; }
    }

}
