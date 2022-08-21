using System.Linq;
using System.Text;

/*
 notes on error: Possibly unhandled rejection:
 as soon as you work with the .$promise, you have to include another .catch() 
 if you don't, then you get the error. 
     */

namespace WEB.Models
{
    public partial class Code
    {
        public string GenerateEnums()
        {
            var s = new StringBuilder();

            s.Add($"namespace {CurrentEntity.Project.Namespace}.Models");
            s.Add($"{{");
            foreach (var lookup in Lookups.Where(o => !o.IsRoleList))
            {
                s.Add($"    public enum " + lookup.Name);
                s.Add($"    {{");
                var options = lookup.LookupOptions.OrderBy(o => o.SortOrder);
                foreach (var option in options)
                    s.Add($"        {option.Name}{(option.Value.HasValue ? " = " + option.Value : string.Empty)}" + (option == options.Last() ? string.Empty : ","));
                s.Add($"    }}");
                s.Add($"");
            }
            s.Add($"    public static class Extensions");
            s.Add($"    {{");
            foreach (var lookup in Lookups.Where(o => !o.IsRoleList))
            {
                s.Add($"        public static string Label(this {lookup.Name} {lookup.Name.ToCamelCase()})");
                s.Add($"        {{");
                s.Add($"            switch ({lookup.Name.ToCamelCase()})");
                s.Add($"            {{");
                var options = lookup.LookupOptions.OrderBy(o => o.SortOrder);
                foreach (var option in options)
                {
                    s.Add($"                case {lookup.Name}.{option.Name}:");
                    s.Add($"                    return \"{option.FriendlyName.Replace("\"", "\\\"")}\";");
                }
                s.Add($"                default:");
                s.Add($"                    return null;");
                s.Add($"            }}");
                s.Add($"        }}");
                s.Add($"");
            }
            s.Add($"    }}");
            s.Add($"}}");

            return RunCodeReplacements(s.ToString(), CodeType.Enums);
        }
    }
}