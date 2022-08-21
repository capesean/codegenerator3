using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Data.Entity;
using System.IO;

/*
 notes on error: Possibly unhandled rejection:
 as soon as you work with the .$promise, you have to include another .catch() 
 if you don't, then you get the error. 
     */

namespace WEB.Models
{
    public partial class Code
    {
        public string GenerateTypeScriptEnums()
        {
            var s = new StringBuilder();

            s.Add($"export class Enum {{");
            s.Add($"    value: number;");
            s.Add($"    name: string;");
            s.Add($"    label: string;");
            s.Add($"}}");
            s.Add($"");

            foreach (var lookup in Lookups.Where(o => !o.IsRoleList))
            {
                s.Add($"export enum {lookup.PluralName} {{");
                var options = lookup.LookupOptions.OrderBy(o => o.SortOrder);
                foreach (var option in options)
                    s.Add($"    {option.Name}{(option.Value.HasValue ? " = " + option.Value : string.Empty)}" + (option == options.Last() ? string.Empty : ","));
                s.Add($"}}");
                s.Add($"");
            }

            s.Add($"export class Enums {{");
            s.Add($"");
            foreach (var lookup in Lookups.Where(o => !o.IsRoleList))
            {
                s.Add($"     static {lookup.PluralName}: Enum[] = [");
                var options = lookup.LookupOptions.OrderBy(o => o.SortOrder);
                var counter = 0;
                foreach (var option in options)
                {
                    s.Add($"        {{ value: {(option.Value.HasValue ? option.Value : counter)}, name: '{option.Name}', label: '{option.FriendlyName}' }}" + (option == options.Last() ? string.Empty : ","));
                    counter++;
                }
                s.Add($"     ]");
                s.Add($"");
            }
            s.Add($"}}");

            return RunCodeReplacements(s.ToString(), CodeType.TypeScriptEnums);
        }
    }
}