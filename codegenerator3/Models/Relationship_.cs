using System.Linq;

namespace WEB.Models
{
    public partial class Relationship
    {
        public string AppSelector
        {
            get
            {
                return $"<{ParentEntity.Name.Hyphenated().Replace(" ", "-")}-select id=\"{RelationshipFields.Single().ChildField.Name.ToCamelCase()}\" name=\"{RelationshipFields.Single().ChildField.Name.ToCamelCase()}\" [(ngModel)]=\"searchOptions.{RelationshipFields.Single().ChildField.Name.ToCamelCase()}\"></{ParentEntity.Name.Hyphenated().Replace(" ", "-")}-select>";
            }
        }
    }
}
