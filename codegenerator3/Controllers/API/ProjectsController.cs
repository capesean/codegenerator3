using System;
using System.Data.Entity;
using System.Linq;
using System.Web.Http;
using System.Threading.Tasks;
using WEB.Models;

namespace WEB.Controllers
{
    [Authorize, RoutePrefix("api/projects")]
    public class ProjectsController : BaseApiController
    {
        [HttpGet, Route("")]
        public async Task<IHttpActionResult> Search([FromUri] PagingOptions pagingOptions, [FromUri] string q = null)
        {
            IQueryable<Project> results = DbContext.Projects;

            if (!string.IsNullOrWhiteSpace(q))
                results = results.Where(o => o.Name.Contains(q));

            results = results.OrderBy(o => o.Name);

            return Ok((await GetPaginatedResponse(results, pagingOptions)).Select(o => ModelFactory.Create(o)));
        }

        [HttpGet, Route("{projectId:Guid}")]
        public async Task<IHttpActionResult> Get(Guid projectId)
        {
            var project = await DbContext.Projects
                .SingleOrDefaultAsync(o => o.ProjectId == projectId);

            if (project == null)
                return NotFound();

            return Ok(ModelFactory.Create(project));
        }

        [HttpPost, Route("")]
        public async Task<IHttpActionResult> Insert([FromBody] ProjectDTO projectDTO)
        {
            if (projectDTO.ProjectId != Guid.Empty) return BadRequest("Invalid ProjectId");

            return await Save(projectDTO);
        }

        [HttpPost, Route("{projectId:Guid}")]
        public async Task<IHttpActionResult> Update(Guid projectId, [FromBody] ProjectDTO projectDTO)
        {
            if (projectDTO.ProjectId != projectId) return BadRequest("Id mismatch");

            return await Save(projectDTO);
        }

        private async Task<IHttpActionResult> Save(ProjectDTO projectDTO)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            if (await DbContext.Projects.AnyAsync(o => o.Name == projectDTO.Name && !(o.ProjectId == projectDTO.ProjectId)))
                return BadRequest("Name already exists.");

            var isNew = projectDTO.ProjectId == Guid.Empty;

            var userEntity = (Entity)null;
            var fullName = (Field)null;

            Project project;

            using (var dbContextTransaction = DbContext.Database.BeginTransaction())
            {

                if (isNew)
                {
                    project = new Project();

                    project.DateCreated = DateTime.Today;

                    DbContext.Entry(project).State = EntityState.Added;
                }
                else
                {
                    project = await DbContext.Projects.SingleOrDefaultAsync(o => o.ProjectId == projectDTO.ProjectId);

                    if (project == null)
                        return NotFound();

                    DbContext.Entry(project).State = EntityState.Modified;
                }

                ModelFactory.Hydrate(project, projectDTO);
                project.WebPath = project.WebPath ?? "";

                await DbContext.SaveChangesAsync();

                if (isNew)
                {
                    userEntity = new Entity();
                    userEntity.ProjectId = project.ProjectId;
                    userEntity.Name = "User";
                    userEntity.PluralName = "Users";
                    userEntity.FriendlyName = "User";
                    userEntity.PluralFriendlyName = "Users";
                    userEntity.EntityType = EntityType.User;
                    userEntity.PartialEntityClass = true;
                    userEntity.AuthorizationType = AuthorizationType.ProtectChanges;
                    userEntity.PreventSortHtmlDeployment = "Not applicable (no sort field)";
                    userEntity.PreventSortTypeScriptDeployment = "Not applicable (no sort field)";
                    DbContext.Entry(userEntity).State = EntityState.Added;

                    await DbContext.SaveChangesAsync();

                    var fieldOrder = 0;

                    DbContext.Entry(
                        new Field
                        {
                            EntityId = userEntity.EntityId,
                            Name = "Id",
                            Label = "User Id",
                            FieldType = FieldType.Guid,
                            KeyField = true,
                            ShowInSearchResults = false,
                            SearchType = SearchType.None,
                            EditPageType = EditPageType.Normal,
                            FieldOrder = fieldOrder++
                        }
                    ).State = EntityState.Added;

                    await DbContext.SaveChangesAsync();

                    DbContext.Entry(
                        new Field
                        {
                            EntityId = userEntity.EntityId,
                            Name = "FirstName",
                            Label = "First Name",
                            FieldType = FieldType.nVarchar,
                            Length = 50,
                            ShowInSearchResults = true,
                            SearchType = SearchType.None,
                            EditPageType = EditPageType.Normal,
                            FieldOrder = fieldOrder++,
                            SortPriority = 1,
                            SortDescending = false
                        }
                    ).State = EntityState.Added;

                    await DbContext.SaveChangesAsync();

                    DbContext.Entry(
                        new Field
                        {
                            EntityId = userEntity.EntityId,
                            Name = "LastName",
                            Label = "Last Name",
                            FieldType = FieldType.nVarchar,
                            Length = 50,
                            ShowInSearchResults = true,
                            SearchType = SearchType.None,
                            EditPageType = EditPageType.Normal,
                            FieldOrder = fieldOrder++,
                            SortPriority = 2,
                            SortDescending = false
                        }
                    ).State = EntityState.Added;

                    await DbContext.SaveChangesAsync();

                    fullName = new Field
                    {
                        EntityId = userEntity.EntityId,
                        Name = "FullName",
                        Label = "Full Name",
                        FieldType = FieldType.nVarchar,
                        Length = 250,
                        ShowInSearchResults = false,
                        SearchType = SearchType.Text,
                        EditPageType = EditPageType.CalculatedField,
                        CalculatedFieldDefinition = "FirstName + ' ' + LastName",
                        FieldOrder = fieldOrder++
                    };
                    userEntity.PrimaryFieldId = fullName.FieldId;

                    DbContext.Entry(fullName).State = EntityState.Added;

                    await DbContext.SaveChangesAsync();

                    DbContext.Entry(
                        new Field
                        {
                            EntityId = userEntity.EntityId,
                            Name = "Email",
                            Label = "Email",
                            FieldType = FieldType.nVarchar,
                            Length = 256,
                            ShowInSearchResults = true,
                            SearchType = SearchType.Text,
                            EditPageType = EditPageType.Normal,
                            FieldOrder = fieldOrder++,
                            RegexValidation = @"^[^@\s]+@[^@\s]+\.[^@\s]+$"
                        }
                    ).State = EntityState.Added;

                    await DbContext.SaveChangesAsync();

                    DbContext.Entry(
                        new Field
                        {
                            EntityId = userEntity.EntityId,
                            Name = "Disabled",
                            Label = "Disabled",
                            FieldType = FieldType.Bit,
                            ShowInSearchResults = true,
                            SearchType = SearchType.Exact,
                            EditPageType = EditPageType.Normal,
                            FieldOrder = fieldOrder++
                        }
                    ).State = EntityState.Added;

                    await DbContext.SaveChangesAsync();

                    var lookup = new Lookup
                    {
                        ProjectId = project.ProjectId,
                        Name = "Role",
                        PluralName = "Roles",
                        IsRoleList = true
                    };

                    DbContext.Entry(lookup).State = EntityState.Added;

                    await DbContext.SaveChangesAsync();

                    DbContext.Entry(new LookupOption { LookupId = lookup.LookupId, Name = "Administrator", FriendlyName = "Administrator" }).State = EntityState.Added;

                    await DbContext.SaveChangesAsync();

                    var settingsEntity = new Entity();
                    settingsEntity.ProjectId = project.ProjectId;
                    settingsEntity.Name = "Settings";
                    settingsEntity.PluralName = "Settings";
                    settingsEntity.FriendlyName = "Settings";
                    settingsEntity.PluralFriendlyName = "Settings";
                    settingsEntity.EntityType = EntityType.Settings;
                    settingsEntity.AuthorizationType = AuthorizationType.ProtectAll;
                    settingsEntity.PreventListHtmlDeployment = "N/A";
                    settingsEntity.PreventListTypeScriptDeployment = "N/A";
                    settingsEntity.PreventAppSelectHtmlDeployment = "N/A";
                    settingsEntity.PreventAppSelectTypeScriptDeployment = "N/A";
                    settingsEntity.PreventSelectModalHtmlDeployment = "N/A";
                    settingsEntity.PreventSelectModalTypeScriptDeployment = "N/A";
                    settingsEntity.PreventSortHtmlDeployment = "N/A";
                    settingsEntity.PreventSortTypeScriptDeployment = "N/A";
                    settingsEntity.PreventSearchOptionsDeployment = "N/A";
                    DbContext.Entry(settingsEntity).State = EntityState.Added;

                    DbContext.Entry(
                        new Field
                        {
                            EntityId = settingsEntity.EntityId,
                            Name = "SetupCompleted",
                            Label = "Setup Completed",
                            FieldType = FieldType.Bit,
                            ShowInSearchResults = false,
                            SearchType = SearchType.None,
                            EditPageType = EditPageType.ReadOnly,
                            FieldOrder = 1
                        }
                    ).State = EntityState.Added;

                    fieldOrder = 1;

                    await DbContext.SaveChangesAsync();

                    DbContext.Entry(
                        new Field
                        {
                            EntityId = settingsEntity.EntityId,
                            Name = "Id",
                            Label = "Id",
                            FieldType = FieldType.Guid,
                            ShowInSearchResults = false,
                            SearchType = SearchType.None,
                            EditPageType = EditPageType.Normal,
                            KeyField = true,
                            FieldOrder = fieldOrder++
                        }
                    ).State = EntityState.Added;

                    var testSetting = new Field
                    {
                        EntityId = settingsEntity.EntityId,
                        Name = "TestSetting",
                        Label = "Test Setting",
                        FieldType = FieldType.nVarchar,
                        Length = 100,
                        ShowInSearchResults = true,
                        SearchType = SearchType.Text,
                        EditPageType = EditPageType.Normal,
                        FieldOrder = fieldOrder++
                    };

                    DbContext.Entry(testSetting).State = EntityState.Added;

                    settingsEntity.PrimaryFieldId = testSetting.FieldId;

                    await DbContext.SaveChangesAsync();
                }

                dbContextTransaction.Commit();
            }

            return await Get(project.ProjectId);
        }

        [HttpDelete, Route("{projectId:Guid}")]
        public async Task<IHttpActionResult> Delete(Guid projectId)
        {
            var project = await DbContext.Projects.SingleOrDefaultAsync(o => o.ProjectId == projectId);

            if (project == null)
                return NotFound();

            if (await DbContext.Entities.AnyAsync(o => o.ProjectId == project.ProjectId))
                return BadRequest("Unable to delete the project as it has related entities");

            if (await DbContext.Lookups.AnyAsync(o => o.ProjectId == project.ProjectId))
                return BadRequest("Unable to delete the project as it has related lookups");

            DbContext.Entry(project).State = EntityState.Deleted;

            await DbContext.SaveChangesAsync();

            return Ok();
        }

    }
}
