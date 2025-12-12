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
        public string GenerateTypeScriptModel()
        {
            var relAsParent = CurrentEntity.RelationshipsAsParent
                .Where(r => r.RelationshipAncestorLimit != RelationshipAncestorLimits.Exclude)
                .Where(o => !o.ChildEntity.Exclude)
                .OrderBy(r => r.SortOrderOnChild)
                .ThenBy(o => o.CollectionName)
                .ToList();

            var s = new StringBuilder();
            s.Add($"import {{ SearchOptions, PagingHeaders }} from './http.model';");
            foreach (var relationshipParentEntity in CurrentEntity.RelationshipsAsChild.Where(r => !r.ParentEntity.Exclude && r.ParentEntityId != CurrentEntity.EntityId).Select(o => o.ParentEntity).Distinct().OrderBy(o => o.Name))
            {
                s.Add($"import {{ {relationshipParentEntity.TypeScriptName} }} from './{relationshipParentEntity.Name.ToLower()}.model';");
            }
            if (CurrentEntity.Fields.Any(o => o.FieldType == FieldType.Enum))
            {
                var lookups = CurrentEntity.Fields.Where(o => o.FieldType == FieldType.Enum).Select(o => o.Lookup.PluralName).OrderBy(o => o).Distinct().Aggregate((current, next) => { return current + ", " + next; });
                s.Add($"import {{ {lookups} }} from './enums.model';");
            }
            foreach (var entity in relAsParent
                .Where(o => o.ChildEntityId != CurrentEntity.EntityId)
                .Select(o => o.ChildEntity)
                .Distinct())
                s.Add($"import {{ {entity.Name} }} from './{entity.Name.ToLower()}.model';");
            s.Add($"");

            s.Add($"export class {CurrentEntity.TypeScriptName} {{");

            // fields
            foreach (var field in CurrentEntity.Fields.Where(o => o.EditPageType != EditPageType.Exclude).OrderBy(f => f.FieldOrder))
            {
                s.Add($"    {field.Name.ToCamelCase()}: {field.JavascriptType};");
            }
            foreach (var relationship in CurrentEntity.RelationshipsAsChild.Where(r => !r.ParentEntity.Exclude)
                .OrderBy(o => o.ParentEntity.Name)
                .ThenBy(o => o.ParentName)
                )
            {
                if (relationship.RelationshipFields.Count == 1 && relationship.RelationshipFields.Single().ChildField.EditPageType == EditPageType.Exclude) continue;
                s.Add($"    {relationship.ParentName.ToCamelCase()}: {relationship.ParentEntity.TypeScriptName};");
            }
            if (CurrentEntity.EntityType == EntityType.User)
            {
                s.Add($"    roles: string[] = [];");
            }
            s.Add($"");

            foreach (var relationship in relAsParent)
            {
                if (!relationship.IsOneToOne)
                    s.Add($"    {relationship.CollectionName.ToCamelCase()}: {relationship.ChildEntity.TypeScriptName}[];");
                else
                    s.Add($"    {relationship.CollectionSingular.ToCamelCase()}: {relationship.ChildEntity.TypeScriptName};");
            }
            if (relAsParent.Where(o => !o.ChildEntity.Exclude).Any())
                s.Add($"");

            s.Add($"    constructor() {{");
            // can't do this for composite key fields, else they all get set to a value (000-000-000) and 
            // then the angular/typescript validation always passes as the value is not-undefined
            if (CurrentEntity.KeyFields.Count() == 1 && CurrentEntity.KeyFields.First().CustomType == CustomType.Guid)
                s.Add($"        this.{CurrentEntity.KeyFields.First().Name.ToCamelCase()} = \"00000000-0000-0000-0000-000000000000\";");
            foreach (var field in CurrentEntity.Fields.OrderBy(o => o.FieldOrder))
                if (!string.IsNullOrWhiteSpace(field.EditPageDefault))
                    s.Add($"        this.{field.Name.ToCamelCase()} = {field.EditPageDefault};");
            foreach (var relationship in relAsParent.Where(o => !o.IsOneToOne))
                s.Add($"        this.{relationship.CollectionName.ToCamelCase()} = [];");
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
            s.Add($"    {CurrentEntity.PluralName.ToCamelCase()}: {CurrentEntity.TypeScriptName}[] = [];");
            s.Add($"    headers: PagingHeaders;");
            s.Add($"}}");

            return RunCodeReplacements(s.ToString(), CodeType.TypeScriptModel);
        }
    }
}
