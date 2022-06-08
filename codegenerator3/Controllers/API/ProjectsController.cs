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
            if (isNew)
            {
                project = new Project();

                project.DateCreated = DateTime.Today;

                DbContext.Entry(project).State = EntityState.Added;

                userEntity = new Entity();
                userEntity.ProjectId = project.ProjectId;
                userEntity.Name = "User";
                userEntity.PluralName = "Users";
                userEntity.FriendlyName = "User";
                userEntity.PluralFriendlyName = "Users";
                userEntity.EntityType = EntityType.User;
                userEntity.PartialEntityClass = true;
                userEntity.AuthorizationType = AuthorizationType.ProtectChanges;
                DbContext.Entry(userEntity).State = EntityState.Added;

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

                DbContext.Entry(fullName).State = EntityState.Added;

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

                var lookup = new Lookup
                {
                    ProjectId = project.ProjectId,
                    Name = "Role",
                    PluralName = "Roles",
                    IsRoleList = true
                };

                DbContext.Entry(lookup).State = EntityState.Added;

                DbContext.Entry(new LookupOption { LookupId = lookup.LookupId, Name = "Administrator", FriendlyName = "Administrator" }).State = EntityState.Added;

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
                userEntity.PrimaryFieldId = fullName.FieldId;
                DbContext.Entry(userEntity).State = EntityState.Modified;
                await DbContext.SaveChangesAsync();
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
