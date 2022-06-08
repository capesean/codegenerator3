using System;
using System.Data.Entity;
using System.Linq;
using System.Web.Http;
using System.Threading.Tasks;
using WEB.Models;

namespace WEB.Controllers
{
    [Authorize, RoutePrefix("api/entities")]
    public partial class EntitiesController : BaseApiController
    {
        [HttpGet, Route("")]
        public async Task<IHttpActionResult> Search([FromUri]PagingOptions pagingOptions, [FromUri]string q = null, [FromUri]Guid? projectId = null, [FromUri]Guid? primaryFieldId = null)
        {
            if (pagingOptions == null) pagingOptions = new PagingOptions();

            IQueryable<Entity> results = DbContext.Entities;
            if (pagingOptions.IncludeEntities)
            {
                results = results.Include(o => o.Project);
                results = results.Include(o => o.PrimaryField);
            }

            if (!string.IsNullOrWhiteSpace(q))
                results = results.Where(o => o.Name.Contains(q));

            if (projectId.HasValue) results = results.Where(o => o.ProjectId == projectId);
            if (primaryFieldId.HasValue) results = results.Where(o => o.PrimaryFieldId == primaryFieldId);

            results = results.OrderBy(o => o.Name);

            return Ok((await GetPaginatedResponse(results, pagingOptions)).Select(o => ModelFactory.Create(o)));
        }

        [HttpGet, Route("{entityId:Guid}")]
        public async Task<IHttpActionResult> Get(Guid entityId)
        {
            var entity = await DbContext.Entities
                .Include(o => o.PrimaryField)
                .Include(o => o.Project)
                .SingleOrDefaultAsync(o => o.EntityId == entityId);

            if (entity == null)
                return NotFound();

            return Ok(ModelFactory.Create(entity));
        }

        [HttpPost, Route("")]
        public async Task<IHttpActionResult> Insert([FromBody]EntityDTO entityDTO)
        {
            if (entityDTO.EntityId != Guid.Empty) return BadRequest("Invalid EntityId");

            return await Save(entityDTO);
        }

        [HttpPost, Route("{entityId:Guid}")]
        public async Task<IHttpActionResult> Update(Guid entityId, [FromBody]EntityDTO entityDTO)
        {
            if (entityDTO.EntityId != entityId) return BadRequest("Id mismatch");

            return await Save(entityDTO);
        }

        private async Task<IHttpActionResult> Save(EntityDTO entityDTO)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var isNew = entityDTO.EntityId == Guid.Empty;

            Entity entity;
            if (isNew)
            {
                entity = new Entity();

                DbContext.Entry(entity).State = EntityState.Added;
            }
            else
            {
                entity = await DbContext.Entities.SingleOrDefaultAsync(o => o.EntityId == entityDTO.EntityId);

                if (entity == null)
                    return NotFound();

                DbContext.Entry(entity).State = EntityState.Modified;
            }

            ModelFactory.Hydrate(entity, entityDTO);

            await DbContext.SaveChangesAsync();

            return await Get(entity.EntityId);
        }

        [HttpDelete, Route("{entityId:Guid}")]
        public async Task<IHttpActionResult> Delete(Guid entityId)
        {
            var entity = await DbContext.Entities.SingleOrDefaultAsync(o => o.EntityId == entityId);

            if (entity == null)
                return NotFound();

            if (await DbContext.Relationships.AnyAsync(o => o.ParentEntityId == entity.EntityId))
                return BadRequest("Unable to delete the entity as it has related relationships");

            if (await DbContext.Relationships.AnyAsync(o => o.ChildEntityId == entity.EntityId))
                return BadRequest("Unable to delete the entity as it has related relationships");

            entity.PrimaryFieldId = null;
            DbContext.Entry(entity).State = EntityState.Modified;
            await DbContext.SaveChangesAsync();

            foreach (var field in DbContext.Fields.Where(o => o.EntityId == entityId).ToList())
                DbContext.Entry(field).State = EntityState.Deleted;

            foreach (var codeReplacement in DbContext.CodeReplacements.Where(o => o.EntityId == entityId).ToList())
                DbContext.Entry(codeReplacement).State = EntityState.Deleted;

            DbContext.Entry(entity).State = EntityState.Deleted;

            await DbContext.SaveChangesAsync();

            return Ok();
        }

    }
}
