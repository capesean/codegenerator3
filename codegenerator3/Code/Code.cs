using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Data.Entity;
using System.IO;
using System.Web.UI;

/*
 notes on error: Possibly unhandled rejection:
 as soon as you work with the .$promise, you have to include another .catch() 
 if you don't, then you get the error. 
     */

namespace WEB.Models
{
    public partial class Code
    {
        private Entity CurrentEntity { get; set; }
        private List<Entity> _allEntities { get; set; }
        private List<Entity> NormalEntities { get { return AllEntities.Where(e => (e.EntityType == EntityType.Normal || e.EntityType == EntityType.Settings) && !e.Exclude).ToList(); } }
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
                s.Add($"        " + roleLookup.LookupOptions.OrderBy(o => o.SortOrder).Select(o => o.Name).Aggregate((current, next) => { return current + ", " + next; }));
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

        private void WriteChildRoutes(IEnumerable<Relationship> relationships, StringBuilder s, int level, string menu)
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
                if (!string.IsNullOrWhiteSpace(menu))
                    s.Add(tabs + $"                            menu: '{menu}',");
                s.Add(tabs + $"                            submenu: '{(string.IsNullOrWhiteSpace(entity.Submenu) ? entity.PluralName.ToCamelCase() : entity.Submenu)}',");
                s.Add(tabs + $"                            breadcrumb: 'Add {entity.FriendlyName}'");
                s.Add(tabs + $"                        }}" + (childRelationships.Any() ? "," : ""));
                if (childRelationships.Any())
                {
                    WriteChildRoutes(childRelationships, s, level + 1, menu);
                }
                s.Add(tabs + $"                    }}" + (relationship == relationships.OrderBy(o => o.ChildEntity.Name).Last() ? "" : ","));
            }
            s.Add(tabs + $"                ]");
        }

        private string GetRouterLink(Entity entity, Entity sourceEntity)
        {
            // reason for change!!
            // change to use relative routes, so I can inject a url prefix (start app at www.site.com/app/<here>)
            // note: this requirement can't work with base href=/app, as that gets set for the entire site, where 
            // the ktu-covid project was effectively 2 websites in 1: /offline and /admin - both needing base href=/

            string routerLink = string.Empty;

            // source entity is the same, i.e. from list page to edit page
            if (sourceEntity.EntityId == entity.EntityId)
            {
                var hierarchicalRelationship = entity.RelationshipsAsChild.SingleOrDefault(r => r.Hierarchy);

                // if the entity is not the child in any relationships, then the link is just a relative link
                if (hierarchicalRelationship == null)
                {
                    routerLink = $"[{entity.KeyFields.Select(o => $"{entity.Name.ToCamelCase()}.{o.Name.ToCamelCase()}").Aggregate((current, next) => { return current + ", " + next; })}], {{ relativeTo: this.route }}";
                }
                // navigating from the list page to the edit page but the entity is a hierarchical child...
                else
                {
                    var prefix = "";

                    while (hierarchicalRelationship != null)
                    {
                        var child = hierarchicalRelationship.ChildEntity;
                        var parent = hierarchicalRelationship.ParentEntity;
                        var keyFields = child.GetNonHierarchicalKeyFields();
                        var keyFieldsRoute = keyFields.Select(o => $"{prefix + child.Name.ToCamelCase()}.{o.Name.ToCamelCase()}").Aggregate((current, next) => { return current + ", " + next; });
                        var parentName = hierarchicalRelationship.ParentName;

                        routerLink = $"\"{child.PluralName.ToLower()}\", " + keyFieldsRoute + (routerLink == string.Empty ? "" : ", ") + routerLink;

                        prefix += hierarchicalRelationship.ChildEntity.Name.ToCamelCase() + ".";

                        hierarchicalRelationship = parent.RelationshipsAsChild.SingleOrDefault(r => r.Hierarchy);

                        if (hierarchicalRelationship == null)
                        {
                            keyFields = parent.GetNonHierarchicalKeyFields();
                            keyFieldsRoute = keyFields.Select(o => $"{prefix + parentName.ToCamelCase()}.{o.Name.ToCamelCase()}").Aggregate((current, next) => { return current + ", " + next; });
                            routerLink = $"[\"/{parent.PluralName.ToLower()}\", " + keyFieldsRoute + ", " + routerLink + "]";
                        }

                    }
                }
            }
            else
            {
                if (entity.RelationshipsAsChild.Any(r => r.Hierarchy))
                {
                    var hierarchicalRelationship = entity.RelationshipsAsChild.Where(o => o.Hierarchy).Single();
                    if (hierarchicalRelationship == null) hierarchicalRelationship = entity.RelationshipsAsParent.Where(o => o.Hierarchy).Single();

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
            }

            return routerLink;
        }

        private string RunTemplateReplacements(string input)
        {
            if (CurrentEntity.KeyFields.Count > 1 && input.Contains("KEYFIELD"))
            {
                // input type=hidden uses KEYFIELD, so app selects can't use where they would return more than 1 key field
                throw new Exception("Unable to run key field replacements (multiple keys). Disable app-selects & sort if not required? " + CurrentEntity.Name);
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
                .Replace("TYPESCRIPTNAME", CurrentEntity.TypeScriptName)
                .Replace("NAME", CurrentEntity.Name)
                .Replace("STARTSWITHVOWEL", new Regex("^[aeiou]").IsMatch(CurrentEntity.Name.ToLower()) && CurrentEntity.Name != "User" ? "n" : "")
                .Replace("ICONLINK", GetIconLink(CurrentEntity))
                .Replace("ADDNEWURL", "/" + CurrentEntity.PluralName.ToLower() + "/add")
                .Replace("// <reference", "/// <reference");
        }

        private string GetIconLink(Entity entity)
        {
            if (string.IsNullOrWhiteSpace(entity.IconClass)) return string.Empty;

            string html = $@"<a routerLink=""{GetHierarchyString(entity)}"" class=""btn btn-secondary"" *ngIf=""!multiple && !!{entity.Name.ToCamelCase()}"" [ngClass]=""{{ 'disabled': disabled }}"">
        <i class=""fas {entity.IconClass}""></i>
    </a>
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
            if (!String.IsNullOrWhiteSpace(CurrentEntity.PreventGeneratedModuleDeployment) && type == CodeType.GeneratedModule) return code;
            if (!String.IsNullOrWhiteSpace(CurrentEntity.PreventSharedModuleDeployment) && type == CodeType.SharedModule) return code;
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
            if (type == CodeType.Enums || type == CodeType.AppRouter || type == CodeType.GeneratedModule || type == CodeType.SharedModule || type == CodeType.DbContext || type == CodeType.SharedModule)
                replacements = CodeReplacements.Where(cr => !cr.Disabled && cr.CodeType == type && cr.Entity.ProjectId == CurrentEntity.ProjectId).ToList();

            foreach (var replacement in replacements.OrderBy(o => o.CodeType == CodeType.Global ? 0 : 1).ThenBy(o => o.SortOrder))
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
            // not 100% sure if this is right when using IncludeAllParents - e.g. Datum->Indicators. Changed that one to include Parent
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
            if (deploymentOptions.GeneratedModule && !string.IsNullOrWhiteSpace(entity.PreventGeneratedModuleDeployment))
                return ("Generated Module deployment is not allowed: " + entity.PreventGeneratedModuleDeployment);
            if (deploymentOptions.SharedModule && !string.IsNullOrWhiteSpace(entity.PreventSharedModuleDeployment))
                return ("Shared Module deployment is not allowed: " + entity.PreventSharedModuleDeployment);
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
            if (deploymentOptions.SortHtml && !string.IsNullOrWhiteSpace(entity.PreventSortHtmlDeployment))
                return ("SortHtml deployment is not allowed: " + entity.PreventSortHtmlDeployment);
            if (deploymentOptions.SortTypeScript && !string.IsNullOrWhiteSpace(entity.PreventSortTypeScriptDeployment))
                return ("SortTypeScript deployment is not allowed: " + entity.PreventSortTypeScriptDeployment);
            if (deploymentOptions.SearchOptions && !string.IsNullOrWhiteSpace(entity.PreventSearchOptionsDeployment))
                return ("SearchOptions deployment is not allowed: " + entity.PreventSearchOptionsDeployment);

            if (deploymentOptions.DbContext)
            {
                var firstEntity = DbContext.Entities.SingleOrDefault(e => !e.Exclude && e.ProjectId == entity.ProjectId && e.PreventDbContextDeployment.Length > 0);
                if (firstEntity != null)
                    return ("DbContext deployment is not allowed on " + firstEntity.Name + ": " + firstEntity.PreventDbContextDeployment);
            }
            if (deploymentOptions.GeneratedModule)
            {
                var firstEntity = DbContext.Entities.SingleOrDefault(e => !e.Exclude && e.ProjectId == entity.ProjectId && e.PreventGeneratedModuleDeployment.Length > 0);
                if (firstEntity != null)
                    return ("Generated Module deployment is not allowed on " + firstEntity.Name + ": " + firstEntity.PreventGeneratedModuleDeployment);
            }
            if (deploymentOptions.SharedModule)
            {
                var firstEntity = DbContext.Entities.SingleOrDefault(e => !e.Exclude && e.ProjectId == entity.ProjectId && e.PreventSharedModuleDeployment.Length > 0);
                if (firstEntity != null)
                    return ("Shared Module deployment is not allowed on " + firstEntity.Name + ": " + firstEntity.PreventSharedModuleDeployment);
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
                if (!Directory.Exists(entity.Project.RootPathModels))
                    return ("Models path does not exist: " + entity.Project.RootPathModels);

                var code = codeGenerator.GenerateModel();
                if (code != string.Empty) File.WriteAllText(Path.Combine(entity.Project.RootPathModels, entity.Name + ".cs"), code);
            }
            #endregion

            #region typescript model
            if (deploymentOptions.TypeScriptModel)
            {
                var path = Path.Combine(entity.Project.RootPathWeb, @"ClientApp\src\app\common\models");
                if (!Directory.Exists(path)) return "ClientApp models path does not exist:" + path;

                var err = WriteFile(path, null, entity.Name.ToLower() + ".model.ts", codeGenerator.GenerateTypeScriptModel());
                if (err != null) return err;
            }
            #endregion

            #region dbcontext
            if (deploymentOptions.DbContext)
            {
                if (!Directory.Exists(entity.Project.RootPathModels))
                    return ("Models path does not exist: " + entity.Project.RootPathModels);

                var code = codeGenerator.GenerateDbContext();
                if (code != string.Empty) File.WriteAllText(Path.Combine(entity.Project.RootPathModels, "ApplicationDBContext_.cs"), code);
            }
            #endregion

            #region dto
            if (deploymentOptions.DTO)
            {
                if (!Directory.Exists(entity.Project.RootPathDTOs))
                    return ("DTOs path does not exist: " + entity.Project.RootPathDTOs);

                var code = codeGenerator.GenerateDTO();
                if (code != string.Empty) File.WriteAllText(Path.Combine(entity.Project.RootPathDTOs, entity.Name + "DTO.cs"), code);
            }
            #endregion

            #region enums
            if (deploymentOptions.Enums)
            {
                // generate C# enums
                if (!Directory.Exists(entity.Project.RootPathModels))
                    return ("Models path does not exist: " + entity.Project.RootPathModels);

                var code = codeGenerator.GenerateEnums();
                if (code != string.Empty) File.WriteAllText(Path.Combine(entity.Project.RootPathModels, "Enums.cs"), code);

                code = codeGenerator.GenerateRoles();
                if (code != string.Empty) File.WriteAllText(Path.Combine(entity.Project.RootPathModels, "Roles.cs"), code);

                // generate TypeScript enums
                var path = Path.Combine(entity.Project.RootPathWeb, @"ClientApp\src\app\common\models");
                if (!Directory.Exists(path)) return "ClientApp Models path does not exist:" + path;

                var err = WriteFile(path, null, "enums.model.ts", codeGenerator.GenerateTypeScriptEnums());
                if (err != null) return err;

            }
            #endregion

            #region settings
            if (deploymentOptions.SettingsDTO)
            {
                if (!Directory.Exists(entity.Project.RootPathDTOs))
                    return ("Settings path does not exist: " + entity.Project.RootPathDTOs);

                // settings now manually done
                //var code = codeGenerator.GenerateSettingsDTO();
                //if (code != string.Empty) File.WriteAllText(Path.Combine(path, "SettingsDTO_.cs"), code);
            }
            #endregion

            #region settings dto
            if (deploymentOptions.SettingsDTO)
            {
                if (!Directory.Exists(entity.Project.RootPathDTOs))
                    return ("Settings DTOs path does not exist: " + entity.Project.RootPathDTOs);

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
                var path = Path.Combine(entity.Project.RootPathWeb, @"ClientApp\src\app", entity.Project.GeneratedPath ?? string.Empty);
                if (!Directory.Exists(path)) return "Generated path does not exist:" + path;

                var err = WriteFile(path, entity.PluralName.ToLower(), entity.Name.ToLower() + ".list.component.html", codeGenerator.GenerateListHtml());
                if (err != null) return err;
            }
            #endregion

            #region list typescript
            if (deploymentOptions.ListTypeScript)
            {
                var path = Path.Combine(entity.Project.RootPathWeb, @"ClientApp\src\app", entity.Project.GeneratedPath ?? string.Empty);
                if (!Directory.Exists(path)) return "Generated path does not exist:" + path;

                var err = WriteFile(path, entity.PluralName.ToLower(), entity.Name.ToLower() + ".list.component.ts", codeGenerator.GenerateListTypeScript());
                if (err != null) return err;
            }
            #endregion

            #region edit html
            if (deploymentOptions.EditHtml)
            {
                var path = Path.Combine(entity.Project.RootPathWeb, @"ClientApp\src\app", entity.Project.GeneratedPath ?? string.Empty);
                if (!Directory.Exists(path)) return "Generated path does not exist:" + path;

                var err = WriteFile(path, entity.PluralName.ToLower(), entity.Name.ToLower() + ".edit.component.html", codeGenerator.GenerateEditHtml());
                if (err != null) return err;
            }
            #endregion

            #region edit typescript
            if (deploymentOptions.EditTypeScript)
            {
                var path = Path.Combine(entity.Project.RootPathWeb, @"ClientApp\src\app", entity.Project.GeneratedPath ?? string.Empty);
                if (!Directory.Exists(path)) return "Generated path does not exist:" + path;

                var err = WriteFile(path, entity.PluralName.ToLower(), entity.Name.ToLower() + ".edit.component.ts", codeGenerator.GenerateEditTypeScript());
                if (err != null) return err;
            }
            #endregion

            #region api resource
            if (deploymentOptions.ApiResource)
            {
                var path = Path.Combine(entity.Project.RootPathWeb, @"ClientApp\src\app\common\services");
                if (!Directory.Exists(path)) return "Services path does not exist:" + path;

                var err = WriteFile(path, null, entity.Name.ToLower() + ".service.ts", codeGenerator.GenerateApiResource());
                if (err != null) return err;
            }
            #endregion

            #region generated module
            if (deploymentOptions.GeneratedModule)
            {
                var path = entity.Project.RootPathWeb + @"ClientApp\src\app\";

                var code = codeGenerator.GenerateGeneratedModule();
                if (code != string.Empty) File.WriteAllText(Path.Combine(path, "generated.module.ts"), code);
            }
            #endregion

            #region shared module
            if (deploymentOptions.SharedModule)
            {
                var path = entity.Project.RootPathWeb + @"ClientApp\src\app\";

                var code = codeGenerator.GenerateSharedModule();
                if (code != string.Empty) File.WriteAllText(Path.Combine(path, "shared.module.ts"), code);
            }
            #endregion

            #region app router
            if (deploymentOptions.AppRouter)
            {
                var path = entity.Project.RootPathWeb + @"ClientApp\src\app\";

                var code = codeGenerator.GenerateRoutes();
                if (code != string.Empty) File.WriteAllText(Path.Combine(path, "generated.routes.ts"), code);
            }
            #endregion

            #region app-select html
            if (deploymentOptions.AppSelectHtml)
            {
                var path = Path.Combine(entity.Project.RootPathWeb, @"ClientApp\src\app", entity.Project.GeneratedPath ?? string.Empty);
                if (!Directory.Exists(path)) return "Generated path does not exist:" + path;

                var err = WriteFile(path, entity.PluralName.ToLower(), entity.Name.ToLower() + ".select.component.html", codeGenerator.GenerateSelectHtml());
                if (err != null) return err;
            }
            #endregion

            #region app-select typescript
            if (deploymentOptions.AppSelectTypeScript)
            {
                var path = Path.Combine(entity.Project.RootPathWeb, @"ClientApp\src\app", entity.Project.GeneratedPath ?? string.Empty);
                if (!Directory.Exists(path)) return "Generated path does not exist:" + path;

                var err = WriteFile(path, entity.PluralName.ToLower(), entity.Name.ToLower() + ".select.component.ts", codeGenerator.GenerateSelectTypeScript());
                if (err != null) return err;
            }
            #endregion

            #region select modal html
            if (deploymentOptions.SelectModalHtml)
            {
                var path = Path.Combine(entity.Project.RootPathWeb, @"ClientApp\src\app", entity.Project.GeneratedPath ?? string.Empty);
                if (!Directory.Exists(path)) return "Generated path does not exist:" + path;

                var err = WriteFile(path, entity.PluralName.ToLower(), entity.Name.ToLower() + ".modal.component.html", codeGenerator.GenerateModalHtml());
                if (err != null) return err;
            }
            #endregion

            #region select modal typescript
            if (deploymentOptions.SelectModalTypeScript)
            {
                var path = Path.Combine(entity.Project.RootPathWeb, @"ClientApp\src\app", entity.Project.GeneratedPath ?? string.Empty);
                if (!Directory.Exists(path)) return "Generated path does not exist:" + path;

                var err = WriteFile(path, entity.PluralName.ToLower(), entity.Name.ToLower() + ".modal.component.ts", codeGenerator.GenerateModalTypeScript());
                if (err != null) return err;
            }
            #endregion

            #region sort html
            if (deploymentOptions.SortHtml && entity.HasASortField)
            {
                //if (!entity.HasASortField)
                //    return ($"Entity {entity.FriendlyName} does not have a sort field. Either add a field with Edit Page Type: Sort, or enter a Prevent Sort Html Deployment comment");

                var path = Path.Combine(entity.Project.RootPathWeb, @"ClientApp\src\app", entity.Project.GeneratedPath ?? string.Empty);
                if (!Directory.Exists(path)) return "Generated path does not exist:" + path;

                var err = WriteFile(path, entity.PluralName.ToLower(), entity.Name.ToLower() + ".sort.component.html", codeGenerator.GenerateSortHtml());
                if (err != null) return err;
            }
            #endregion

            #region sort typescript
            if (deploymentOptions.SortTypeScript && entity.HasASortField)
            {
                //if (!entity.HasASortField)
                //    return ($"Entity {entity.FriendlyName} does not have a sort field. Either add a field with Edit Page Type: Sort, or enter a Prevent Sort Html Deployment comment");

                var path = Path.Combine(entity.Project.RootPathWeb, @"ClientApp\src\app", entity.Project.GeneratedPath ?? string.Empty);
                if (!Directory.Exists(path)) return "Generated path does not exist:" + path;

                var err = WriteFile(path, entity.PluralName.ToLower(), entity.Name.ToLower() + ".sort.component.ts", codeGenerator.GenerateSortTypeScript());
                if (err != null) return err;
            }
            #endregion

            #region search options
            if (deploymentOptions.SearchOptions)
            {
                if (!Directory.Exists(entity.Project.RootPathSearchOptions))
                    return ("SearchOptions path does not exist: " + entity.Project.RootPathSearchOptions);

                var code = codeGenerator.GenerateSearchOptions();
                if (code != string.Empty) File.WriteAllText(Path.Combine(entity.Project.RootPathSearchOptions, entity.Name + "SearchOptions.cs"), code);
            }
            #endregion

            return null;
        }

        private static string WriteFile(string path, string folder, string fileName, string code)
        {
            if (code == string.Empty) return null;

            if (!Directory.Exists(path)) return "Path does not exist: " + path;

            if (!string.IsNullOrWhiteSpace(folder)) path = Path.Combine(path, folder);
            if (!Directory.Exists(path)) Directory.CreateDirectory(path);

            File.WriteAllText(Path.Combine(path, fileName), code);
            return null;
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
        public bool GeneratedModule { get; set; }
        public bool SharedModule { get; set; }
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
        public bool SortHtml { get; set; }
        public bool SortTypeScript { get; set; }
        public bool SearchOptions { get; set; }
    }

}
