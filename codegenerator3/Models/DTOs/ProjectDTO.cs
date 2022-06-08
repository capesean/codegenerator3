using System;
using System.ComponentModel.DataAnnotations;

namespace WEB.Models
{
    public class ProjectDTO
    {
        [Required]
        public Guid ProjectId { get; set; }

        [DisplayFormat(ConvertEmptyStringToNull = false)]
        [MaxLength(50)]
        public string Name { get; set; }

        [DisplayFormat(ConvertEmptyStringToNull = false)]
        [MaxLength(250)]
        public string WebPath { get; set; }

        [DisplayFormat(ConvertEmptyStringToNull = false)]
        [MaxLength(20)]
        public string Namespace { get; set; }

        [DisplayFormat(ConvertEmptyStringToNull = false)]
        [MaxLength(20)]
        public string AngularModuleName { get; set; }

        [DisplayFormat(ConvertEmptyStringToNull = false)]
        [MaxLength(20)]
        public string AngularDirectivePrefix { get; set; }

        [Required]
        public bool Bootstrap3 { get; set; }

        [MaxLength(50)]
        public string UrlPrefix { get; set; }

        [Required]
        public bool UseStringAuthorizeAttributes { get; set; }

        [DisplayFormat(ConvertEmptyStringToNull = false)]
        [MaxLength(20)]
        public string DbContextVariable { get; set; }

        [MaxLength(50)]
        public string UserFilterFieldName { get; set; }

        [MaxLength(50)]
        public string ModelsPath { get; set; }

        public string Notes { get; set; }

        [MaxLength(20)]
        public string RouteViewName { get; set; }

    }

    public partial class ModelFactory
    {
        public ProjectDTO Create(Project project)
        {
            if (project == null) return null;

            var projectDTO = new ProjectDTO();

            projectDTO.ProjectId = project.ProjectId;
            projectDTO.Name = project.Name;
            projectDTO.WebPath = project.WebPath;
            projectDTO.Namespace = project.Namespace;
            projectDTO.AngularModuleName = project.AngularModuleName;
            projectDTO.AngularDirectivePrefix = project.AngularDirectivePrefix;
            projectDTO.UserFilterFieldName = project.UserFilterFieldName;
            projectDTO.DbContextVariable = project.DbContextVariable;
            projectDTO.ModelsPath = project.ModelsPath;
            projectDTO.Notes = project.Notes;

            return projectDTO;
        }

        public void Hydrate(Project project, ProjectDTO projectDTO)
        {
            project.Name = projectDTO.Name;
            project.WebPath = projectDTO.WebPath;
            project.Namespace = projectDTO.Namespace;
            project.AngularModuleName = projectDTO.AngularModuleName;
            project.AngularDirectivePrefix = projectDTO.AngularDirectivePrefix;
            project.UserFilterFieldName = projectDTO.UserFilterFieldName;
            project.DbContextVariable = projectDTO.DbContextVariable;
            project.ModelsPath = projectDTO.ModelsPath;
            project.Notes = projectDTO.Notes;
        }
    }
}
