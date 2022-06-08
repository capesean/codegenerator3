using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WEB.Models
{
    public partial class Relationship
    {
        [Key]
        [Required]
        public Guid RelationshipId { get; set; }

        [Required]
        public Guid ParentEntityId { get; set; }

        [Required]
        public Guid ChildEntityId { get; set; }

        [Required(AllowEmptyStrings = true)]
        [MaxLength(50)]
        public string CollectionName { get; set; }

        [Required(AllowEmptyStrings = true)]
        [MaxLength(50)]
        public string CollectionFriendlyName { get; set; }

        [Required(AllowEmptyStrings = true)]
        [MaxLength(50)]
        public string ParentName { get; set; }

        [Required(AllowEmptyStrings = true)]
        [MaxLength(50)]
        public string ParentFriendlyName { get; set; }

        [Required]
        public Guid ParentFieldId { get; set; }

        [Required]
        public bool DisplayListOnParent { get; set; }

        [Required]
        public bool Hierarchy { get; set; }

        [Required]
        public int SortOrder { get; set; }

        [Required]
        public RelationshipAncestorLimits RelationshipAncestorLimit { get; set; }

        [Required]
        public bool CascadeDelete { get; set; }

        [Required]
        public bool UseSelectorDirective { get; set; }

        [Required]
        public bool UseMultiSelect { get; set; }

        [Required]
        public bool IsOneToOne { get; set; }

        [Required]
        public int SortOrderOnChild { get; set; }

        public virtual ICollection<RelationshipField> RelationshipFields { get; set; } = new List<RelationshipField>();

        [ForeignKey("ChildEntityId")]
        public virtual Entity ChildEntity { get; set; }

        [ForeignKey("ParentEntityId")]
        public virtual Entity ParentEntity { get; set; }

        [ForeignKey("ParentFieldId")]
        public virtual Field ParentField { get; set; }

        public Relationship()
        {
            RelationshipId = Guid.NewGuid();
        }

        public override string ToString()
        {
            return CollectionName;
        }
    }
}
