using System;
using System.Data.Entity;
using System.Linq;
using System.Web.Http;
using System.Threading.Tasks;
using WEB.Models;
using System.Collections.Generic;

namespace WEB.Controllers
{
    [Authorize, RoutePrefix("api/utilities")]
    public class UtilitiesController : BaseApiController
    {
        [HttpPost, Route("multideploy")]
        public async Task<IHttpActionResult> MultiDeploy([FromBody] List<Option> options)
        {
            if (!System.Web.HttpContext.Current.Request.IsLocal)
                return BadRequest("Deployment is only allowed when hosted on a local machine");

            var badEntity = await DbContext.Entities.FirstOrDefaultAsync(o => !o.Exclude && o.PrimaryFieldId == null);
            if (badEntity != null) return BadRequest(badEntity.Name + " doesn't have a Primary Field");

            var results = new List<DeploymentResult>();

            bool enumsHasRun = false;
            bool settingsdtoHasRun = false;
            bool dbcontextHasRun = false;
            bool bundleconfigHasRun = false;
            bool approuterHasRun = false;

            foreach (var option in options)
            {
                if (option.ApiResource
                    || option.AppRouter
                    || option.BundleConfig
                    || option.Controller
                    || option.DbContext
                    || option.DTO
                    || option.EditHtml
                    || option.EditTypeScript
                    || option.Enums
                    || option.ListHtml
                    || option.ListTypeScript
                    || option.Model
                    || option.TypeScriptModel
                    || option.SettingsDTO
                    || option.AppSelectHtml
                    || option.AppSelectTypeScript
                    || option.SelectModalHtml
                    || option.SelectModalTypeScript
                    )
                {

                    var entity = DbContext.Entities
                        .Include(o => o.Project)
                        .Include(o => o.Fields)
                        .Include(o => o.CodeReplacements)
                        .Include(o => o.RelationshipsAsChild.Select(p => p.RelationshipFields))
                        .Include(o => o.RelationshipsAsChild.Select(p => p.ParentEntity))
                        .Include(o => o.RelationshipsAsParent.Select(p => p.RelationshipFields))
                        .Include(o => o.RelationshipsAsParent.Select(p => p.ChildEntity))
                        .SingleOrDefault(o => o.EntityId == option.EntityId);

                    if (entity == null) return BadRequest("Invalid Entity Id");

                    if (entity.Exclude) continue;

                    if (option.Model) RunDeploy(entity, CodeType.Model, results);
                    if (option.TypeScriptModel) RunDeploy(entity, CodeType.TypeScriptModel, results);
                    if (option.Enums && !enumsHasRun) { RunDeploy(entity, CodeType.Enums, results); enumsHasRun = true; }
                    if (option.DTO) RunDeploy(entity, CodeType.DTO, results);
                    if (option.SettingsDTO && !settingsdtoHasRun) { RunDeploy(entity, CodeType.SettingsDTO, results); settingsdtoHasRun = false; }
                    if (option.DbContext && !dbcontextHasRun) { RunDeploy(entity, CodeType.DbContext, results); dbcontextHasRun = true; }
                    if (option.Controller) RunDeploy(entity, CodeType.Controller, results);
                    if (option.BundleConfig && !bundleconfigHasRun) { RunDeploy(entity, CodeType.BundleConfig, results); bundleconfigHasRun = true; }
                    if (option.AppRouter && !approuterHasRun) { RunDeploy(entity, CodeType.AppRouter, results); approuterHasRun = true; }
                    if (option.ApiResource) RunDeploy(entity, CodeType.ApiResource, results);
                    if (option.ListHtml) RunDeploy(entity, CodeType.ListHtml, results);
                    if (option.ListTypeScript) RunDeploy(entity, CodeType.ListTypeScript, results);
                    if (option.EditHtml) RunDeploy(entity, CodeType.EditHtml, results);
                    if (option.EditTypeScript) RunDeploy(entity, CodeType.EditTypeScript, results);
                    if (option.AppSelectHtml) RunDeploy(entity, CodeType.AppSelectHtml, results);
                    if (option.AppSelectTypeScript) RunDeploy(entity, CodeType.AppSelectTypeScript, results);
                    if (option.SelectModalHtml) RunDeploy(entity, CodeType.SelectModalHtml, results);
                    if (option.SelectModalTypeScript) RunDeploy(entity, CodeType.SelectModalTypeScript, results);
                }
            }

            return Ok(results);
        }

        private void RunDeploy(Entity entity, CodeType codeType, List<DeploymentResult> results)
        {
            var options = new DeploymentOptions();
            options.Model = codeType == CodeType.Model;
            options.TypeScriptModel = codeType == CodeType.TypeScriptModel;
            options.Enums = codeType == CodeType.Enums;
            options.DTO = codeType == CodeType.DTO;
            options.SettingsDTO = codeType == CodeType.SettingsDTO;
            options.DbContext = codeType == CodeType.DbContext;
            options.Controller = codeType == CodeType.Controller;
            options.BundleConfig = codeType == CodeType.BundleConfig;
            options.AppRouter = codeType == CodeType.AppRouter;
            options.ApiResource = codeType == CodeType.ApiResource;
            options.ListHtml = codeType == CodeType.ListHtml;
            options.ListTypeScript = codeType == CodeType.ListTypeScript;
            options.EditHtml = codeType == CodeType.EditHtml;
            options.EditTypeScript = codeType == CodeType.EditTypeScript;
            options.AppSelectHtml = codeType == CodeType.AppSelectHtml;
            options.AppSelectTypeScript = codeType == CodeType.AppSelectTypeScript;
            options.SelectModalHtml = codeType == CodeType.SelectModalHtml;
            options.SelectModalTypeScript = codeType == CodeType.SelectModalTypeScript;

            var result = Code.RunDeployment(DbContext, entity, options);
            results.Add(new DeploymentResult
            {
                EntityName = entity.Name,
                CodeType = codeType.Label(),
                isError = result != null,
                ErrorMessage = result
            });
        }
    }

    public class DeploymentResult
    {
        public string EntityName;
        public string CodeType;
        public string ErrorMessage;
        public bool isError;
    }

    public class Option
    {
        public Guid EntityId { get; set; }
        public bool Model { get; set; } = false;
        public bool TypeScriptModel { get; set; } = false;
        public bool Enums { get; set; } = false;
        public bool DTO { get; set; } = false;
        public bool SettingsDTO { get; set; } = false;
        public bool DbContext { get; set; } = false;
        public bool Controller { get; set; } = false;
        public bool BundleConfig { get; set; } = false;
        public bool AppRouter { get; set; } = false;
        public bool ApiResource { get; set; } = false;
        public bool ListHtml { get; set; } = false;
        public bool ListTypeScript { get; set; } = false;
        public bool EditHtml { get; set; } = false;
        public bool EditTypeScript { get; set; } = false;
        public bool AppSelectHtml { get; set; } = false;
        public bool AppSelectTypeScript { get; set; } = false;
        public bool SelectModalHtml { get; set; } = false;
        public bool SelectModalTypeScript { get; set; } = false;

    }
}
