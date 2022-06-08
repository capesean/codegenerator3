using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WEB.Models
{
    public partial class Entity
    {
        [Key]
        [Required]
        public Guid EntityId { get; set; }

        [Required]
        public Guid ProjectId { get; set; }

        [Required(AllowEmptyStrings = true)]
        [MaxLength(50)]
        public string Name { get; set; }

        [Required(AllowEmptyStrings = true)]
        [MaxLength(50)]
        public string PluralName { get; set; }

        [Required(AllowEmptyStrings = true)]
        [MaxLength(50)]
        public string FriendlyName { get; set; }

        [Required(AllowEmptyStrings = true)]
        [MaxLength(50)]
        public string PluralFriendlyName { get; set; }

        [Required]
        public EntityType EntityType { get; set; }

        [Required]
        public bool PartialEntityClass { get; set; }

        [Required]
        public bool PartialControllerClass { get; set; }

        [MaxLength(250)]
        public string Breadcrumb { get; set; }

        [MaxLength(100)]
        public string PreventModelDeployment { get; set; }

        [MaxLength(100)]
        public string PreventDTODeployment { get; set; }

        [MaxLength(100)]
        public string PreventDbContextDeployment { get; set; }

        [MaxLength(100)]
        public string PreventControllerDeployment { get; set; }

        [MaxLength(100)]
        public string PreventBundleConfigDeployment { get; set; }

        [MaxLength(100)]
        public string PreventAppRouterDeployment { get; set; }

        [MaxLength(100)]
        public string PreventApiResourceDeployment { get; set; }

        [MaxLength(100)]
        public string PreventListHtmlDeployment { get; set; }

        [MaxLength(100)]
        public string PreventListTypeScriptDeployment { get; set; }

        [MaxLength(100)]
        public string PreventEditHtmlDeployment { get; set; }

        [MaxLength(100)]
        public string PreventEditTypeScriptDeployment { get; set; }

        [MaxLength(100)]
        public string PreventAppSelectHtmlDeployment { get; set; }

        [MaxLength(100)]
        public string PreventAppSelectTypeScriptDeployment { get; set; }

        [MaxLength(100)]
        public string PreventSelectModalHtmlDeployment { get; set; }

        [MaxLength(100)]
        public string PreventSelectModalTypeScriptDeployment { get; set; }

        [Required]
        public AuthorizationType AuthorizationType { get; set; }

        [Required]
        public bool Exclude { get; set; }

        [Required]
        public bool ReturnOnSave { get; set; }

        [Required]
        public bool UseChildRoutes { get; set; }

        public Guid? PrimaryFieldId { get; set; }

        [MaxLength(30)]
        public string IconClass { get; set; }

        [MaxLength(100)]
        public string UserFilterFieldPath { get; set; }

        public virtual ICollection<Relationship> RelationshipsAsParent { get; set; } = new List<Relationship>();

        public virtual ICollection<Field> Fields { get; set; } = new List<Field>();

        public virtual ICollection<Relationship> RelationshipsAsChild { get; set; } = new List<Relationship>();

        public virtual ICollection<CodeReplacement> CodeReplacements { get; set; } = new List<CodeReplacement>();

        [ForeignKey("PrimaryFieldId")]
        public virtual Field PrimaryField { get; set; }

        [ForeignKey("ProjectId")]
        public virtual Project Project { get; set; }

        public Entity()
        {
            EntityId = Guid.NewGuid();
        }

        public override string ToString()
        {
            return Name;
        }
    }
}
