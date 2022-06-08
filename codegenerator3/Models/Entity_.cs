using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Runtime.InteropServices;

namespace WEB.Models
{
    public partial class Entity
    {
        [NotMapped]
        public virtual List<Field> KeyFields
        {
            get
            {
                return Fields.Where(o => o.KeyField).OrderBy(o => o.FieldOrder).ToList();
            }
        }

        [NotMapped]
        public virtual List<Field> TextSearchFields
        {
            get
            {
                return Fields.Where(o => o.SearchType == SearchType.Text).OrderBy(o => o.FieldOrder).ToList();
            }
        }

        [NotMapped]
        public virtual List<Field> ExactSearchFields
        {
            get
            {
                return Fields.Where(o => o.SearchType == SearchType.Exact).OrderBy(o => o.FieldOrder).ToList();
            }
        }

        [NotMapped]
        public virtual List<Field> RangeSearchFields
        {
            get
            {
                return Fields.Where(o => o.SearchType == SearchType.Range).OrderBy(o => o.FieldOrder).ToList();
            }
        }

        [NotMapped]
        public virtual List<Field> SortOrderFields
        {
            get
            {
                if (!Fields.Any(f => f.SortPriority.HasValue)) return KeyFields;
                return Fields.Where(o => o.SortPriority.HasValue).OrderBy(o => o.SortPriority).ToList();
            }
        }

        [NotMapped]
        public string RoutePath
        {
            get
            {
                return KeyFields.Select(o => $"{{{o.Name.ToCamelCase() + (o.ControllerConstraintType == "string" ? string.Empty : ":" + o.ControllerConstraintType)}}}").Aggregate((current, next) => current + "/" + next);
            }
        }

        [NotMapped]
        public bool HasUserFilterField
        {
            get { return !string.IsNullOrWhiteSpace(UserFilterFieldPath) && !string.IsNullOrWhiteSpace(Project.UserFilterFieldName); }
        }

        [NotMapped]
        public string ReturnRoute
        {
            get
            {
                // hierarchies have format: /parententities/:parententityid/childentities/:childentityid
                // non-hierarchies have format: /childentities/:childentityid
                // so when in hierarchy, need to move one extra step up (childentities)
                string prefix = "";
                if (RelationshipsAsChild.Any(o => o.Hierarchy))
                    prefix = "../";
                return $"this.router.navigate([\"{prefix}{String.Concat(Enumerable.Repeat("../", GetNonHierarchicalKeyFields().Count))}\"], {{ relativeTo: this.route }});";
            }
        }

        internal List<Field> GetNonHierarchicalKeyFields()
        {
            var parentRel = RelationshipsAsChild.SingleOrDefault(o => o.Hierarchy);
            // exclude parent entity fields
            // example: in African POT, where the Team entity has a composite key of projectid + userid
            //          and the team is a hierarchical child on the project: the url should be '/projects/{projectid}/team/{userid}
            //          without excluding, the url was: '/projects/{projectid}/team/{projectid}/{userid}
            //          i.e. duplicating projectid
            if (parentRel == null || parentRel.IsOneToOne) return KeyFields;
            return KeyFields.Where(o => !o.RelationshipFieldsAsChild.Any(rf => rf.ParentField.EntityId == parentRel.ParentEntityId)).ToList();
        }

        [NotMapped]
        public string ControllerParameters
        {
            get
            {
                return KeyFields.Select(o => string.Format("{0} {1}", o.NetType, o.Name.ToCamelCase())).Aggregate((current, next) => current + ", " + next);
            }
        }

        [NotMapped]
        public List<Entity> RequiredRelationshipEntities
        {
            get
            {
                // exclude UseSelectorDirectives to remove them from the edit typescript controller defn
                // 20180417: if hierarchy, include relationship, so breadcrumb can be set for isnew
                var entities = RelationshipsAsChild.Where(r => !r.ParentEntity.Exclude && r.ParentEntityId != EntityId && (r.Hierarchy || !r.UseSelectorDirective)).OrderBy(o => o.SortOrderOnChild).Select(r => r.ParentEntity).ToList();
                entities.AddRange(RelationshipsAsParent.Where(r => !r.ChildEntity.Exclude && r.DisplayListOnParent).OrderBy(o => o.SortOrder).Select(r => r.ChildEntity).ToList());
                return entities.Distinct().ToList();
            }
        }

        [NotMapped]
        public string DTOName
        {
            get
            {
                return Name + "DTO";
            }
        }

        [NotMapped]
        public string DefaultGo
        {
            get
            {
                var navEntities = GetNavigationEntities();
                if (navEntities.Count == 1 && navEntities[0].EntityId == EntityId)
                    return $"this.$state.go(\"app.{PluralName.ToCamelCase()}\")";
                else
                {
                    var navFields = GetNavigationFields();
                    var url = string.Empty;
                    foreach (var field in navFields)
                    {
                        if (field.EntityId == EntityId) continue;
                        url += (url == string.Empty ? string.Empty : ", ") + field.Name.ToCamelCase() + $": this.$stateParams.{field.Name.ToCamelCase()}";
                    }
                    url = $"this.$state.go(\"app.{navEntities[navEntities.Count - 2].Name.ToCamelCase()}\", {{ {url} }})";
                    return url;
                }
            }
        }

        public class SearchResultColumn
        {
            public string Header { get; set; }
            public string Value { get; set; }
            public bool IsOnAnotherEntity { get; set; }
        }

        internal Relationship GetParentSearchRelationship(Field field)
        {
            // field must be in a relationship of this entity, where the field is (part of) the key of the related entity
            var relationship = RelationshipsAsChild.Single(r => r.RelationshipFields.Any(f => f.ChildFieldId == field.FieldId && f.ParentField.KeyField));
            //if (relationship.RelationshipFields.Count() != 1) throw new Exception("Can't have a search field that is part of a multi-field key");
            return relationship;
        }

        internal List<SearchResultColumn> GetSearchResultsFields(Entity currentEntity)
        {
            var result = new List<SearchResultColumn>();
            foreach (var field in Fields.Where(f => f.ShowInSearchResults).OrderBy(f => f.FieldOrder))
            {
                if (RelationshipsAsChild.Any(r => r.RelationshipFields.Any(f => f.ChildFieldId == field.FieldId)))
                {
                    var rel = RelationshipsAsChild.Single(r => r.RelationshipFields.Any(f => f.ChildFieldId == field.FieldId));
                    if (rel.ParentEntity == currentEntity) continue;

                    var value = string.Empty;
                    if (rel.ParentEntity.PrimaryField.FieldType == FieldType.Enum)
                    {
                        if (field.IsNullable) throw new Exception("unhandled: what now!?");
                        value = $"{{{{ {rel.ParentEntity.PrimaryField.Lookup.PluralName.ToCamelCase()}[{ field.Entity.Name.ToCamelCase()}.{ rel.ParentName.ToCamelCase() }.{ rel.ParentField.Name.ToCamelCase() }].label }}}}";
                    }
                    else
                    {
                        value = $"{{{{ { field.Entity.Name.ToCamelCase()}.{ rel.ParentName.ToCamelCase() + (field.IsNullable ? "?" : "") }.{ rel.ParentField.Name.ToCamelCase() } }}}}";
                    }

                    result.Add(new SearchResultColumn
                    {
                        Header = rel.ParentEntity.FriendlyName,
                        Value = value,
                        IsOnAnotherEntity = true
                    });
                }
                else
                {
                    var column = new SearchResultColumn { Header = field.Label, IsOnAnotherEntity = false };
                    // these should use local formats?
                    if (field.FieldType == FieldType.Enum)
                        column.Value = $"{{{{ {field.Lookup.PluralName.ToCamelCase()}[{field.Entity.Name.ToCamelCase()}.{ field.Name.ToCamelCase() }].label }}}}";
                    else if (field.FieldType == FieldType.Bit)
                        column.Value = $"{{{{ { field.Entity.Name.ToCamelCase()}.{ field.Name.ToCamelCase() } | booleanPipe }}}}";
                    else if (field.FieldType == FieldType.Money)
                        column.Value = $"{{{{ { field.Entity.Name.ToCamelCase()}.{ field.Name.ToCamelCase() } | currency }}}}";
                    else if (field.FieldType == FieldType.Date)
                        column.Value = $"{{{{ { field.Entity.Name.ToCamelCase()}.{ field.Name.ToCamelCase() } | momentPipe: 'DD MMM YYYY' }}}}";
                    else if (field.FieldType == FieldType.DateTime || field.FieldType == FieldType.SmallDateTime)
                        column.Value = $"{{{{ { field.Entity.Name.ToCamelCase()}.{ field.Name.ToCamelCase() } | momentPipe: 'DD MMM YYYY HH:mm' }}}}";
                    else
                        column.Value = $"{{{{ { field.Entity.Name.ToCamelCase()}.{ field.Name.ToCamelCase() } }}}}";
                    result.Add(column);
                }
            }

            return result;
        }

        internal List<Field> GetNavigationFields()
        {
            // was: FirstOrDefault
            var hierarchy = RelationshipsAsChild.SingleOrDefault(r => r.Hierarchy);

            var result = new List<Field>();
            foreach (var field in KeyFields.OrderBy(f => f.SortPriority))
            {
                // exclude field if hierarchichal parent has field of same name
                // this is used to ignore/exclude a multi-field primary key on an entity that
                // is a hierarchical child of the repeated parent key.
                // example: WRC project: customer is hierarchical parent of consumption. 
                //          key on consumption = customer + consumptionSet.
                //          url must NOT be: customer/:customerId/consumption/:customerId/:consumptionSetId
                //          as that would repeat the :customerId param, which is not allowed.
                //  because it is hierarchical, just need the parent's :customerId

                if (hierarchy == null || !hierarchy.ParentEntity.KeyFields.Any(f => f.Name == field.Name))
                    result.Add(field);
            }

            if (hierarchy != null)
            {
                var parentResult = hierarchy.ParentEntity.GetNavigationFields();
                result.InsertRange(0, parentResult);
            }

            return result;
        }

        internal List<Entity> GetNavigationEntities(bool reverse = false)
        {
            var entities = GetNavigationFields().Select(f => f.Entity).Distinct().ToList();
            if (reverse) entities.Reverse();
            return entities;
        }

        internal string GetNavigationUrl()
        {
            // builds the string for the app-router.ts, in the format: /parent1Name/:parent1Key1/:parent1Key2/entity1Name/:key1/:key2
            var result = string.Empty;

            var navigationFields = GetNavigationFields();
            Entity lastEntity = null;
            foreach (var field in navigationFields)
            {
                if (lastEntity != field.Entity) result += "/" + field.Entity.PluralName.ToLower();
                var fieldName = field.Name.ToCamelCase();
                result += "/:" + fieldName;
                lastEntity = field.Entity;
            }

            return result;
        }

        internal string GetGoToEntityCode()
        {
            var navFields = GetNavigationFields();

            //string result = string.Empty;
            //Entity lastEntity = null;
            //var fields = new HashSet<string>();
            //foreach (var field in navFields)
            //{
            //    if (lastEntity != field.Entity) result += "/" + field.Entity.PluralName.ToLower();
            //    var fieldName = field.Name.ToCamelCase();
            //    // ideally, we should traverse the hierarchical relationship, but GetNavigationFields doesn't have this, so a simple check:
            //    if (!fields.Contains(fieldName))
            //    {
            //        result += "/:" + fieldName;
            //        fields.Add(fieldName);
            //    }
            //    lastEntity = field.Entity;
            //}

            string fieldList = $"{navFields.Select(o => o.Name.ToCamelCase()).Aggregate((current, next) => current + ", " + next) }";
            string parameters = $"{{ {navFields.Select(o => o.Name.ToCamelCase() + ": " + o.Name.ToCamelCase()).Aggregate((current, next) => current + ", " + next) } }}";

            return $"goTo{Name} = ({fieldList}) => this.$state.go(\"app.{Name.ToCamelCase()}\", {parameters});";
        }

        internal string GetNavigationString()
        {
            // builds the parameters for the vm.goToEntity() call in the html (NOT the method in typescript)
            // in the format: "entity.parent1.key1, entity.parent1.key2, entity.key1, entity.key2"
            var navigationString = string.Empty;
            foreach (var field in GetNavigationFields())
            {
                var reversedNavFields = GetNavigationFields();
                reversedNavFields.Reverse();
                var prefix = string.Empty;
                Entity lastEntity = null;
                foreach (var f in reversedNavFields)
                {
                    if (f.Entity == lastEntity) continue;

                    prefix += f.Entity.Name.ToCamelCase() + ".";

                    if (f.Entity == field.Entity) break;
                    lastEntity = f.Entity;
                }
                navigationString += (navigationString == string.Empty ? string.Empty : ", ") + prefix + field.Name.ToCamelCase();
            }
            return navigationString;
        }

        [NotMapped]
        internal string ResourceName
        {
            get
            {
                if (EntityType == EntityType.User) return "userResource";
                return Name.ToCamelCase() + "Resource";
            }
        }

        [NotMapped]
        internal string ClassResourceName
        {
            get
            {
                if (EntityType == EntityType.User) return "this.userResource";
                return "this." + Name.ToCamelCase() + "Resource";
            }
        }

        [NotMapped]
        internal string TypeScriptResource
        {
            get
            {
                return "Models." + Name + "Resource";
            }
        }

        [NotMapped]
        internal string TypeScriptDTO
        {
            get
            {
                return "Models." + Name + "DTO";
            }
        }

        [NotMapped]
        internal string CamelCaseName
        {
            get
            {
                return Name.ToCamelCase();
            }
        }

        [NotMapped]
        internal string ClassModelObject
        {
            get
            {
                return "this." + Name.ToCamelCase();
            }
        }

        [NotMapped]
        internal string ClassModelObjects
        {
            get
            {
                return "this." + PluralName.ToCamelCase();
            }
        }

        [NotMapped]
        internal string ViewModelObject
        {
            get
            {
                return Name.ToCamelCase();
            }
        }

        internal bool HasASortField
        {
            get
            {
                return SortField != null;
            }
        }

        internal bool HasCompositePrimaryKey
        {
            get
            {
                // checks for composite primary key, i.e. a key made up of foreign keys to other entities,
                // like a fact key being the combination of the munic, year, & indicator entities.
                var returnVal = true;

                if (KeyFields.Count <= 1) return false;

                foreach (var field in KeyFields)
                {
                    if (!RelationshipsAsChild.Any(r => r.RelationshipFields.Any(f => f.ChildFieldId == field.FieldId)))
                        returnVal = false;
                }


                return returnVal;
            }
        }

        internal bool HasAppSelects(ApplicationDbContext dbContext)
        {
            return dbContext.Relationships.Any(o => o.ParentEntityId == EntityId && o.UseSelectorDirective);
        }

        internal Field SortField
        {
            get
            {
                var sortFields = Fields.Where(f => f.EditPageType == EditPageType.SortField).ToList();

                if (sortFields.Count == 0) return null;

                if (sortFields.Count > 1)
                    throw new InvalidOperationException("Only 1 sort field is allowed per entity");

                if (sortFields.Count == 1)
                {
                    if (sortFields[0].FieldType != FieldType.Int) throw new InvalidOperationException("Sortable fields must be of type Int");
                    //if (KeyFields.Count() > 1) throw new InvalidOperationException("Sortable is only allowed on single-keyed entities");
                    if (KeyFields[0].FieldType != FieldType.Guid) throw new InvalidOperationException("Sortable is only allowed on Guid-keyed entities");
                }

                return sortFields.Single();

                //return Fields.SingleOrDefault(f => f.EditPageType == EditPageType.SortField);
            }
        }
    }
}
