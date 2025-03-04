﻿using Microsoft.Ajax.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace WEB.Models
{
    public partial class Code
    {
        public string GenerateController()
        {
            var s = new StringBuilder();

            var fileContentsFields = CurrentEntity.Fields.Where(o => o.EditPageType == EditPageType.FileContents).ToList();
            if (fileContentsFields.Count > 1) throw new NotImplementedException("More than one File Contents field per entity");
            var fileContentsField = fileContentsFields.FirstOrDefault();

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
                s.Add($"        private RoleManager<Role> rm;");
                s.Add($"        private IOptions<IdentityOptions> opts;");
                s.Add($"");
                s.Add($"        public UsersController(IDbContextFactory<ApplicationDbContext> dbFactory, UserManager<User> um, AppSettings appSettings, RoleManager<Role> rm, IOptions<IdentityOptions> opts)");
                s.Add($"            : base(dbFactory, um, appSettings) {{ this.rm = rm; this.opts = opts; }}");
            }
            else
            {
                s.Add($"        public {CurrentEntity.PluralName}Controller(IDbContextFactory<ApplicationDbContext> dbFactory, UserManager<User> um, AppSettings appSettings) : base(dbFactory, um, appSettings) {{ }}");
            }
            s.Add($"");

            #region search
            if (CurrentEntity.EntityType != EntityType.Settings)
            {
                s.Add($"        [HttpGet{(CurrentEntity.AuthorizationType == AuthorizationType.ProtectAll ? ", AuthorizeRoles(Roles.Administrator)" : string.Empty)}]");

                var roleSearch = string.Empty;
                if (CurrentEntity.EntityType == EntityType.User)
                    roleSearch = ", [FromQuery] string roleName = null";


                s.Add($"        public async Task<IActionResult> Search([FromQuery] {CurrentEntity.Name}SearchOptions searchOptions{roleSearch})");
                s.Add($"        {{");

                if (CurrentEntity.EntityType == EntityType.User)
                {
                    s.Add($"            IQueryable<User> results = userManager.Users;");
                    if (CurrentEntity.HasUserFilterField)
                        s.Add($"            results = results.Where(o => o.{CurrentEntity.UserFilterFieldPath} == CurrentUser.{CurrentEntity.Project.UserFilterFieldName});");
                    s.Add($"            results = results.Include(o => o.Roles);");
                    s.Add($"");
                }
                else
                {
                    s.Add($"            IQueryable<{CurrentEntity.Name}> results = {CurrentEntity.Project.DbContextVariable}.{CurrentEntity.PluralName}{(CurrentEntity.HasUserFilterField ? "" : ";")}");
                    if (CurrentEntity.HasUserFilterField)
                        s.Add($"                .Where(o => o.{CurrentEntity.UserFilterFieldPath} == CurrentUser.{CurrentEntity.Project.UserFilterFieldName});");
                    s.Add($"");
                }

                var relAsChild = CurrentEntity.RelationshipsAsChild
                    .Where(r => r.RelationshipAncestorLimit != RelationshipAncestorLimits.Exclude)
                    .OrderBy(r => r.SortOrderOnChild)
                    .ThenBy(o => o.ParentName)
                    .ToList();
                if (relAsChild.Any())
                {
                    s.Add($"            if (searchOptions.IncludeParents)");
                    s.Add($"            {{");
                    foreach (var relationship in relAsChild)
                    {
                        foreach (var result in GetTopAncestors(new List<string>(), "o", relationship, relationship.RelationshipAncestorLimit, includeIfHierarchy: true))
                            s.Add($"                results = results.Include(o => {result});");
                    }
                    s.Add($"            }}");
                    s.Add($"");
                }

                var relAsParent = CurrentEntity.RelationshipsAsParent
                    .Where(r => r.RelationshipAncestorLimit != RelationshipAncestorLimits.Exclude)
                    .Where(r => !r.ChildEntity.Exclude)
                    .OrderBy(r => r.SortOrderOnChild)
                    .ThenBy(o => o.CollectionName)
                    .ToList();
                if (relAsParent.Any())
                {
                    s.Add($"            if (searchOptions.IncludeChildren)");
                    s.Add($"            {{");
                    foreach (var relationship in relAsParent)
                    {
                        if (!relationship.IsOneToOne)
                            s.Add($"                results = results.Include(o => o.{relationship.CollectionName});");
                        //else
                        //    s.Add($"                results = results.Include(o => o.{relationship.CollectionSingular});");
                    }
                    s.Add($"            }}");
                    s.Add($"");
                }

                if (CurrentEntity.TextSearchFields.Count > 0)
                {
                    s.Add($"            if (!string.IsNullOrWhiteSpace(searchOptions.q))");
                    s.Add($"                results = results.Where(o => {(CurrentEntity.EntityType == EntityType.User ? "o.Email.Contains(searchOptions.q) || " : "")}{CurrentEntity.TextSearchFields.Select(o => $"o.{o.Name + (o.CustomType == CustomType.String ? string.Empty : ".ToString()")}.Contains(searchOptions.q)").Aggregate((current, next) => current + " || " + next)});");
                    s.Add($"");
                }

                if (CurrentEntity.EntityType == EntityType.User)
                {
                    s.Add($"            if (!string.IsNullOrWhiteSpace(roleName))");
                    s.Add($"            {{");
                    s.Add($"                var role = await rm.Roles.SingleOrDefaultAsync(o => o.Name == roleName);");
                    s.Add($"                results = results.Where(o => o.Roles.Any(r => r.RoleId == role.Id));");
                    s.Add($"            }}");
                    s.Add($"");
                }

                var fieldsToSearch = CurrentEntity.AllNonTextSearchableFields;
                if (fieldsToSearch.Count > 0)
                {
                    foreach (var field in fieldsToSearch)
                    {
                        if (field.SearchType == SearchType.Range && field.CustomType == CustomType.Date)
                        {
                            //s.Add($"            if (from{field.Name}.HasValue) {{ var from{field.Name}Utc = from{field.Name}.Value.ToUniversalTime(); results = results.Where(o => o.{ field.Name} >= from{field.Name}Utc); }}");
                            //s.Add($"            if (to{field.Name}.HasValue) {{ var to{field.Name}Utc = to{field.Name}.Value.ToUniversalTime(); results = results.Where(o => o.{ field.Name} <= to{field.Name}Utc); }}");
                            // disabled: in covid.distribution, the date is sent as 2020-04-18, so no conversion needed, else it chops off the end date
                            s.Add($"            if (searchOptions.From{field.Name}.HasValue) results = results.Where(o => o.{field.Name} >= searchOptions.From{field.Name}.Value.Date);");
                            s.Add($"            if (searchOptions.To{field.Name}.HasValue) results = results.Where(o => o.{field.Name} < searchOptions.To{field.Name}.Value.Date.AddDays(1));");
                        }
                        else
                        {
                            s.Add($"            if (searchOptions.{field.Name}{(field.CustomType == CustomType.String ? " != null" : ".HasValue")}) results = results.Where(o => o.{field.Name} == searchOptions.{field.Name});");
                        }
                    }
                    s.Add($"");
                }

                var sortableFields = CurrentEntity.Fields.Where(o => o.Sortable).OrderBy(o => o.Name).ToList();
                if (sortableFields.Any())
                {
                    var counter = 0;
                    foreach (var sortableField in sortableFields)
                    {
                        counter++;

                        s.Add($"            {(counter == 1 ? "" : "else ")}if (searchOptions.OrderBy == \"{sortableField.Name.ToLower()}\")");
                        s.Add($"                results = searchOptions.OrderByAscending ? results.OrderBy(o => o.{sortableField.Name}) : results.OrderByDescending(o => o.{sortableField.Name});");
                    }
                    if (CurrentEntity.SortOrderFields.Count > 0)
                        s.Add($"            else");
                }

                if (CurrentEntity.SortOrderFields.Count > 0)
                    s.Add($"            {(sortableFields.Any() ? "    " : "")}results = results.Order{CurrentEntity.SortOrderFields.Select(f => "By" + (f.SortDescending ? "Descending" : string.Empty) + "(o => o." + f.Name + ")").Aggregate((current, next) => current + ".Then" + next)};");

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
                    s.Add($"            return Ok((await GetPaginatedResponse(results, searchOptions)).Select(o => ModelFactory.Create(o, searchOptions.IncludeParents, searchOptions.IncludeChildren, roles)));");
                }
                else
                {
                    s.Add($"            return Ok((await GetPaginatedResponse(results, searchOptions)).Select(o => ModelFactory.Create(o, searchOptions.IncludeParents, searchOptions.IncludeChildren)));");
                }
                s.Add($"        }}");
                s.Add($"");
            }
            #endregion

            #region get
            s.Add($"        [HttpGet{(CurrentEntity.EntityType == EntityType.Settings ? string.Empty : $"(\"{CurrentEntity.RoutePath}\")")}{(CurrentEntity.AuthorizationType == AuthorizationType.ProtectAll ? ", AuthorizeRoles(Roles.Administrator)" : string.Empty)}]");
            s.Add($"        public async Task<IActionResult> Get({(CurrentEntity.EntityType == EntityType.Settings ? string.Empty : CurrentEntity.ControllerParameters)})");
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
            foreach (var relationship in CurrentEntity.RelationshipsAsParent.Where(o => o.IsOneToOne).OrderBy(r => r.SortOrder).ThenBy(o => o.ParentName))
            {
                s.Add($"                .Include(o => o.{relationship.CollectionSingular})");
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
            if (CurrentEntity.EntityType == EntityType.Settings)
                s.Add($"                .SingleAsync();");
            else
                s.Add($"                .FirstOrDefaultAsync(o => {GetKeyFieldLinq("o")});");
            s.Add($"");
            if (CurrentEntity.EntityType != EntityType.Settings)
            {
                s.Add($"            if ({CurrentEntity.CamelCaseName} == null)");
                s.Add($"                return NotFound();");
                s.Add($"");
            }

            var relationshipsAsChild = CurrentEntity.RelationshipsAsChild.OrderBy(o => o.ParentFriendlyName);
            var relationshipsAsParent = CurrentEntity.RelationshipsAsParent.OrderBy(o => o.ParentFriendlyName);

            if (CurrentEntity.EntityType == EntityType.User)
            {
                s.Add($"            var roles = await db.Roles.ToListAsync();");
                s.Add($"");
                s.Add($"            return Ok(ModelFactory.Create({CurrentEntity.CamelCaseName}, dbRoles: roles));");
            }
            else
            {
                s.Add($"            return Ok(ModelFactory.Create({CurrentEntity.CamelCaseName}));");
            }
            s.Add($"        }}");
            s.Add($"");
            #endregion

            #region save
            s.Add($"        [HttpPost{(CurrentEntity.EntityType == EntityType.Settings ? string.Empty : $"(\"{CurrentEntity.RoutePath}\")")}{(CurrentEntity.AuthorizationType == AuthorizationType.None ? "" : ", AuthorizeRoles(Roles.Administrator)")}]");
            s.Add($"        public async Task<IActionResult> Save({(CurrentEntity.EntityType == EntityType.Settings ? string.Empty : CurrentEntity.ControllerParameters + ", ")}[FromBody] {CurrentEntity.DTOName} {CurrentEntity.DTOName.ToCamelCase()})");
            s.Add($"        {{");
            s.Add($"            if (!ModelState.IsValid) return BadRequest(ModelState);");
            s.Add($"");

            if (CurrentEntity.EntityType != EntityType.Settings)
            {
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
                    s.Add($"            var {CurrentEntity.CamelCaseName} = await {CurrentEntity.Project.DbContextVariable}.{CurrentEntity.PluralName}");
                    if (CurrentEntity.HasUserFilterField)
                        s.Add($"                .Where(o => o.{CurrentEntity.UserFilterFieldPath} == CurrentUser.{CurrentEntity.Project.UserFilterFieldName})");
                    s.Add($"                .FirstOrDefaultAsync(o => {GetKeyFieldLinq("o", CurrentEntity.DTOName.ToCamelCase())});");
                    s.Add($"");
                    s.Add($"            var isNew = {CurrentEntity.CamelCaseName} == null;");
                    s.Add($"");
                    s.Add($"            if (isNew)");
                    s.Add($"            {{");
                    s.Add($"                {CurrentEntity.CamelCaseName} = new {CurrentEntity.Name}();");
                    s.Add($"");
                    //foreach (var field in CurrentEntity.Fields.Where(f => f.KeyField && f.Entity.RelationshipsAsChild.Any(r => r.RelationshipFields.Any(rf => rf.ChildFieldId == f.FieldId))).OrderBy(f => f.FieldOrder))
                    // on insert, all primary key fields (in composite keyed entities) come from the dto
                    foreach (var field in CurrentEntity.Fields.Where(f => f.KeyField).OrderBy(f => f.FieldOrder))
                    {
                        s.Add($"                {CurrentEntity.CamelCaseName}.{field.Name} = {CurrentEntity.DTOName.ToCamelCase() + "." + field.Name};");
                    }
                    foreach (var field in CurrentEntity.Fields.Where(f => !string.IsNullOrWhiteSpace(f.ControllerInsertOverride)).OrderBy(f => f.FieldOrder))
                    {
                        s.Add($"                {CurrentEntity.CamelCaseName}.{field.Name} = {field.ControllerInsertOverride};");
                    }
                    if (CurrentEntity.Fields.Any(f => !string.IsNullOrWhiteSpace(f.ControllerInsertOverride)) || CurrentEntity.Fields.Any(f => f.KeyField))
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
                        s.Add($"                password = Utilities.General.GenerateRandomPassword(opts.Value.Password);");
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
                        s.Add($"                user = await userManager.FindByIdAsync(userDTO.Id.ToString());");
                        if (CurrentEntity.HasUserFilterField)
                            throw new Exception("TO FIX");
                        //s.Add($"                    .Where(o => o.{CurrentEntity.UserFilterFieldPath} == CurrentUser.{CurrentEntity.Project.UserFilterFieldName})");
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
            }
            else
            {
                s.Add($"            var {CurrentEntity.CamelCaseName} = await {CurrentEntity.Project.DbContextVariable}.{CurrentEntity.PluralName}");
                s.Add($"                .SingleAsync();");
                s.Add($"");
                s.Add($"            {CurrentEntity.Project.DbContextVariable}.Entry({CurrentEntity.CamelCaseName}).State = EntityState.Modified;");
                s.Add($"");
            }

            var deleteFile = CurrentEntity.HasAFileContentsField && CurrentEntity.HasAzureBlobStorageField && CurrentEntity.Fields.Single(o => o.EditPageType == EditPageType.FileName).IsNullable;
            if (deleteFile)
            {
                var fieldName = CurrentEntity.Fields.Single(o => o.EditPageType == EditPageType.FileName).Name;
                s.Add($"            var deleteFile = {CurrentEntity.CamelCaseName}DTO.{fieldName} == null && {CurrentEntity.CamelCaseName}.{fieldName} != null;");
                s.Add($"");
            }

            s.Add($"            ModelFactory.Hydrate({CurrentEntity.CamelCaseName}, {CurrentEntity.DTOName.ToCamelCase()}{(CurrentEntity.Fields.Any(o => o.EditPageType == EditPageType.EditWhenNew) ? ", isNew" : "")});");
            s.Add($"");

            if (CurrentEntity.HasAFileContentsField && !CurrentEntity.HasAzureBlobStorageField)
            {
                s.Add($"            if ({CurrentEntity.DTOName.ToCamelCase()}.{fileContentsField.Name} != null)");
                s.Add($"            {{");
                s.Add($"                if (isNew)");
                s.Add($"                    db.Entry({CurrentEntity.Name.ToCamelCase()}.{CurrentEntity.Name}Content).State = EntityState.Added;");
                s.Add($"                else");
                s.Add($"                {{");
                s.Add($"                    if (await {CurrentEntity.Project.DbContextVariable}.{CurrentEntity.PluralName}.AnyAsync(o => {GetKeyFieldLinq("o", CurrentEntity.DTOName.ToCamelCase())} && o.{CurrentEntity.Name}Content != null))");
                s.Add($"                        db.Entry({CurrentEntity.Name.ToCamelCase()}.{CurrentEntity.Name}Content).State = EntityState.Modified;");
                s.Add($"                    else");
                s.Add($"                        db.Entry({CurrentEntity.Name.ToCamelCase()}.{CurrentEntity.Name}Content).State = EntityState.Added;");
                s.Add($"                }}");
                s.Add($"            }}");
                s.Add($"");
            }

            if (CurrentEntity.EntityType == EntityType.User)
            {
                s.Add($"            var saveResult = (isNew ? await userManager.CreateAsync(user, password) : await userManager.UpdateAsync(user));");
                s.Add($"");
                s.Add($"            if (!saveResult.Succeeded)");
                s.Add($"                return GetErrorResult(saveResult);");
                s.Add($"");
                s.Add($"            if (!isNew)");
                s.Add($"            {{");
                // changed 1/8/24 based on what works in ixesha-2
                s.Add($"                foreach (var role in await userManager.GetRolesAsync(user))");
                s.Add($"                {{");
                s.Add($"                    await userManager.RemoveFromRoleAsync(user, role);");
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
                s.Add($"            if (isNew) await Utilities.General.SendWelcomeMailAsync(user, password, AppSettings);");
            }
            else
            {
                s.Add($"            await {CurrentEntity.Project.DbContextVariable}.SaveChangesAsync();");
            }

            if (CurrentEntity.HasAFileContentsField && CurrentEntity.HasAzureBlobStorageField)
            {
                s.Add($"");
                s.Add($"            if ({CurrentEntity.DTOName.ToCamelCase()}.{fileContentsField.Name} != null)");
                s.Add($"            {{");
                s.Add($"                var blobStorageService = new BlobStorageService(AppSettings.Azure.Documents.ConnectionString, AppSettings.Azure.Documents.ContainerName);");
                s.Add($"                await blobStorageService.UploadBlobAsync({CurrentEntity.Name.ToCamelCase()}.{CurrentEntity.KeyFields.Single().Name}.ToString().ToLowerInvariant(), Convert.FromBase64String({CurrentEntity.DTOName.ToCamelCase()}.{fileContentsField.Name}));");
                s.Add($"            }}");
                if (deleteFile)
                {
                    s.Add($"            else if (deleteFile)");
                    s.Add($"            {{");
                    s.Add($"                var blobStorageService = new BlobStorageService(AppSettings.Azure.Documents.ConnectionString, AppSettings.Azure.Documents.ContainerName);");
                    s.Add($"                await blobStorageService.DeleteBlobAsync({CurrentEntity.Name.ToCamelCase()}.{CurrentEntity.KeyFields.Single().Name}.ToString().ToLowerInvariant());");
                    s.Add($"            }}");
                }
            }

            s.Add($"");
            s.Add($"            return await Get({(CurrentEntity.EntityType == EntityType.Settings ? string.Empty : CurrentEntity.KeyFields.Select(f => CurrentEntity.CamelCaseName + "." + f.Name).Aggregate((current, next) => current + ", " + next))});");
            s.Add($"        }}");
            s.Add($"");
            #endregion

            if (CurrentEntity.EntityType != EntityType.Settings)
            {

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

                var errorOnDeleteRelationships = CurrentEntity.RelationshipsAsParent.Where(rel => !rel.ChildEntity.Exclude && !rel.CascadeDelete).OrderBy(o => o.SortOrder);
                foreach (var relationship in errorOnDeleteRelationships)
                {
                    var joins = relationship.RelationshipFields.Select(o => $"o.{o.ChildField.Name} == {CurrentEntity.CamelCaseName}.{o.ParentField.Name}").Aggregate((current, next) => current + " && " + next);
                    s.Add($"            if (await {CurrentEntity.Project.DbContextVariable}.{(relationship.ChildEntity.EntityType == EntityType.User ? "Users" : relationship.ChildEntity.PluralName)}.AnyAsync(o => {joins}))");
                    s.Add($"                return BadRequest(\"Unable to delete the {CurrentEntity.FriendlyName.ToLower()} as it has related {relationship.CollectionFriendlyName.ToLower()}\");");
                    s.Add($"");
                }

                var cascadeDeleteRelationships = CurrentEntity.RelationshipsAsParent.Where(rel => !rel.ChildEntity.Exclude && rel.CascadeDelete).OrderBy(o => o.SortOrder);

                var usingTransaction = cascadeDeleteRelationships.Any() || (CurrentEntity.HasAFileContentsField && !CurrentEntity.HasAzureBlobStorageField);
                if (usingTransaction)
                {
                    // using () -> otherwise any db calls outside the transaction will error
                    s.Add($"            using (var transactionScope = Utilities.General.CreateTransactionScope())");
                    s.Add($"            {{");
                }

                if (cascadeDeleteRelationships.Any())
                {
                    foreach (var relationship in cascadeDeleteRelationships)
                    {
                        var joins = relationship.RelationshipFields.Select(o => $"o.{o.ChildField.Name} == {CurrentEntity.CamelCaseName}.{o.ParentField.Name}").Aggregate((current, next) => current + " && " + next);
                        var joinsContents = relationship.RelationshipFields.Select(o => $"o.{relationship.ChildEntity.Name}.{o.ChildField.Name} == {CurrentEntity.CamelCaseName}.{o.ParentField.Name}").Aggregate((current, next) => current + " && " + next);

                        if (relationship.ChildEntity.Fields.Any(o => o.EditPageType == EditPageType.FileContents))
                        {
                            s.Add($"{(usingTransaction ? "    " : "")}            await {CurrentEntity.Project.DbContextVariable}.{(relationship.ChildEntity.EntityType == EntityType.User ? "User" : relationship.ChildEntity.Name)}Contents.Where(o => {joinsContents}).ExecuteDeleteAsync();");
                            s.Add($"");
                        }
                        s.Add($"{(usingTransaction ? "    " : "")}            await {CurrentEntity.Project.DbContextVariable}.{(relationship.ChildEntity.EntityType == EntityType.User ? "Users" : relationship.ChildEntity.PluralName)}.Where(o => {joins}).ExecuteDeleteAsync();");
                        s.Add($"");
                    }
                }

                if (CurrentEntity.HasAFileContentsField && !CurrentEntity.HasAzureBlobStorageField)
                {
                    s.Add($"{(usingTransaction ? "    " : "")}            await {CurrentEntity.Project.DbContextVariable}.{CurrentEntity.Name}Contents.Where(o => {CurrentEntity.KeyFields.Select(o => $"o.{o.Name} == {o.Name.ToCamelCase()}").Aggregate((current, next) => current + " && " + next)}).ExecuteDeleteAsync();");
                    s.Add($"");
                }

                if (CurrentEntity.EntityType == EntityType.User)
                {
                    s.Add($"{(usingTransaction ? "    " : "")}            foreach (var role in await userManager.GetRolesAsync(user))");
                    s.Add($"{(usingTransaction ? "    " : "")}                await userManager.RemoveFromRoleAsync(user, role);");
                    s.Add($"");
                    s.Add($"{(usingTransaction ? "    " : "")}            await userManager.DeleteAsync(user);");
                }
                else
                {
                    s.Add($"{(usingTransaction ? "    " : "")}            {CurrentEntity.Project.DbContextVariable}.Entry({CurrentEntity.CamelCaseName}).State = EntityState.Deleted;");
                    s.Add($"");
                    s.Add($"{(usingTransaction ? "    " : "")}            await {CurrentEntity.Project.DbContextVariable}.SaveChangesAsync();");
                }

                if (usingTransaction)
                {
                    s.Add($"");
                    s.Add($"                transactionScope.Complete();");
                    s.Add($"            }}");
                }

                if (CurrentEntity.HasAzureBlobStorageField)
                {
                    s.Add($"");
                    s.Add($"            var blobStorageService = new BlobStorageService(AppSettings.Azure.Documents.ConnectionString, AppSettings.Azure.Documents.ContainerName);");
                    s.Add($"            await blobStorageService.DeleteBlobAsync({CurrentEntity.KeyFields.Single().Name.ToCamelCase()}.ToString().ToLowerInvariant());");
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
                    var relHierarchy = CurrentEntity.RelationshipsAsChild.SingleOrDefault(o => o.Hierarchy);
                    if (relHierarchy == null)
                        s.Add($"        public async Task<IActionResult> Sort([FromBody] Guid[] sortedIds)");
                    else
                    {
                        var sortFilter = relHierarchy.RelationshipFields.Select(o => $"[FromQuery] {o.ChildField.NetType} {o.ChildField.Name.ToCamelCase()}").Aggregate((current, next) => current + ", " + next);
                        s.Add($"        public async Task<IActionResult> Sort({sortFilter}, [FromBody] Guid[] sortedIds)");
                    }
                    s.Add($"        {{");
                    // if it's a child entity, just sort the id's that were sent
                    if (relHierarchy != null)
                    {
                        var sortFilter = relHierarchy.RelationshipFields.Select(o => $"o.{o.ChildField.Name} == {o.ChildField.Name.ToCamelCase()}").Aggregate((current, next) => current + " && " + next);
                        s.Add($"            var {CurrentEntity.PluralName.ToCamelCase()} = await {CurrentEntity.Project.DbContextVariable}.{CurrentEntity.PluralName}");
                        s.Add($"                .Where(o => {sortFilter})");
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
                foreach (var rel in CurrentEntity.RelationshipsAsParent.Where(r => !r.DisableListDelete && !r.ChildEntity.Exclude && r.DisplayListOnParent).OrderBy(r => r.SortOrder))
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

                    var errorOnParentDeleteRelationships = entity.RelationshipsAsParent.Where(r => !r.ChildEntity.Exclude && !r.CascadeDelete).OrderBy(o => o.SortOrder);
                    foreach (var relationship in errorOnParentDeleteRelationships)
                    {
                        // changed to use field on parent entity, rather than child, to avoid issues where names between child/parent are not the same
                        // changed again for monic: Date entity, deleting a date will delete (eg) quarters in year, date in year, etc. (used code replacements to fix same entity issue + duplication)
                        var joins = rel.RelationshipFields.Select(o => $"o.{relationship.ParentName}.{o.ChildField.Name} == {o.ParentField.Name.ToCamelCase()}").Aggregate((current, next) => current + " && " + next);
                        s.Add($"            if (await {CurrentEntity.Project.DbContextVariable}.{(relationship.ChildEntity.EntityType == EntityType.User ? "Users" : relationship.ChildEntity.PluralName)}.AnyAsync(o => {joins}))");
                        s.Add($"                return BadRequest(\"Unable to delete the {rel.CollectionFriendlyName.ToLower()} as there are related {relationship.ChildEntity.PluralFriendlyName.ToLower()}\");");
                        s.Add($"");
                    }

                    var cascadeOnParentDeleteRelationships = entity.RelationshipsAsParent.Where(r => !r.ChildEntity.Exclude && r.CascadeDelete).OrderBy(o => o.SortOrder);

                    if (entity.HasAzureBlobStorageField)
                    {
                        s.Add($"            var blobStorageService = new BlobStorageService(AppSettings.Azure.Documents.ConnectionString, AppSettings.Azure.Documents.ContainerName);");
                        s.Add($"");
                        s.Add($"            var {entity.KeyFields.Single().Name.ToCamelCase()}s = await db.{entity.PluralName}.Where(o => o.{rel.RelationshipFields.Single().ChildField.Name} == {rel.RelationshipFields.Single().ChildField.Name.ToCamelCase()}).Select(o => o.{entity.KeyFields.Single().Name}).ToListAsync();");
                        s.Add($"");
                    }

                    usingTransaction = cascadeOnParentDeleteRelationships.Any() || (entity.HasAFileContentsField && !entity.HasAzureBlobStorageField);
                    if (usingTransaction)
                    {
                        // using () -> otherwise any db calls outside the transaction will error
                        s.Add($"            using (var transactionScope = Utilities.General.CreateTransactionScope())");
                        s.Add($"            {{");
                    }

                    if (cascadeOnParentDeleteRelationships.Any())
                    {
                        foreach (var relationship in cascadeOnParentDeleteRelationships)
                        {
                            var joins = CurrentEntity.KeyFields.Select(o => $"o.{relationship.ParentName}.{o.Name} == {o.Name.ToCamelCase()}").Aggregate((current, next) => current + " && " + next);

                            if (relationship.ChildEntity.Fields.Any(o => o.EditPageType == EditPageType.FileContents))
                            {
                                // this may not be right?
                                s.Add($"{(usingTransaction ? "    " : "")}            await {CurrentEntity.Project.DbContextVariable}.{(relationship.ChildEntity.EntityType == EntityType.User ? "Users" : relationship.ChildEntity.PluralName)}.Where(o => {joins}).ExecuteDeleteAsync();");
                                //s.Add($"            await db.Database.ExecuteSqlRawAsync($\"DELETE FROM {relationship.ChildEntity.PluralName} WHERE {relationship.RelationshipFields.Select(o => o.ChildField.Name + " = '{" + o.ParentField.Name.ToCamelCase() + "}'").Aggregate((current, next) => { return current + " AND " + next; })}\");");
                            }
                            else
                                s.Add($"{(usingTransaction ? "    " : "")}            await {CurrentEntity.Project.DbContextVariable}.{(relationship.ChildEntity.EntityType == EntityType.User ? "Users" : relationship.ChildEntity.PluralName)}.Where(o => {joins}).ExecuteDeleteAsync();");
                            s.Add($"");
                        }
                    }

                    if (entity.HasAFileContentsField && !entity.HasAzureBlobStorageField)
                    {
                        s.Add($"{(usingTransaction ? "    " : "")}            await {CurrentEntity.Project.DbContextVariable}.{entity.Name}Contents.Where(o => {rel.RelationshipFields.Select(o => $"o.{rel.ChildEntity.Name}." + o.ChildField.Name + " == " + o.ParentField.Name.ToCamelCase()).Aggregate((current, next) => { return current + " && " + next; })}).ExecuteDeleteAsync();");
                        s.Add($"");
                    }
                    s.Add($"{(usingTransaction ? "    " : "")}            await {CurrentEntity.Project.DbContextVariable}.{entity.PluralName}.Where(o => {rel.RelationshipFields.Select(o => "o." + o.ChildField.Name + " == " + o.ParentField.Name.ToCamelCase()).Aggregate((current, next) => { return current + " && " + next; })}).ExecuteDeleteAsync();");
                    s.Add($"");

                    if (usingTransaction)
                    {
                        s.Add($"                transactionScope.Complete();");
                        s.Add($"            }}");
                        s.Add($"");
                    }

                    if (entity.HasAzureBlobStorageField)
                    {
                        s.Add($"            foreach (var {entity.KeyFields.Single().Name.ToCamelCase()} in {entity.KeyFields.Single().Name.ToCamelCase()}s)");
                        s.Add($"                await blobStorageService.DeleteBlobAsync({entity.KeyFields.Single().Name.ToCamelCase()}.ToString().ToLowerInvariant());");
                        s.Add($"");
                    }

                    s.Add($"            return Ok();");
                    s.Add($"        }}");
                    s.Add($"");
                }
                #endregion

            }

            s.Add($"    }}");
            s.Add($"}}");

            return RunCodeReplacements(s.ToString(), CodeType.Controller);
        }
    }
}