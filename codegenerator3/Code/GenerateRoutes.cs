using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Web.UI.WebControls;

namespace WEB.Models
{
    public partial class Code
    {
        public string GenerateRoutes()
        {
            var s = new StringBuilder();

            s.Add($"import {{ Route }} from '@angular/router';");
            s.Add($"import {{ AccessGuard }} from './common/auth/auth.accessguard';");

            var allEntities = AllEntities.Where(e => !e.Exclude).OrderBy(o => o.Name);
            foreach (var entity in allEntities)
            {
                s.Add($"import {{ {entity.Name}ListComponent }} from './{entity.PluralName.ToLower()}/{entity.Name.ToLower()}.list.component';");
                s.Add($"import {{ {entity.Name}EditComponent }} from './{entity.PluralName.ToLower()}/{entity.Name.ToLower()}.edit.component';");
            }
            //s.Add($"import {{ NotFoundComponent }} from './common/notfound.component';");
            s.Add($"");

            s.Add($"export const GeneratedRoutes: Route[] = [");

            foreach (var entity in allEntities.Where(o => !o.Exclude).OrderBy(o => o.Name))
            {
                var editOnRoot = !entity.RelationshipsAsChild.Any(r => r.Hierarchy);
                var childRelationships = entity.RelationshipsAsParent.Where(r => r.Hierarchy);

                s.Add($"    {{");
                s.Add($"        path: '{entity.PluralName.ToLower()}',");
                s.Add($"        canActivate: [AccessGuard],");
                s.Add($"        canActivateChild: [AccessGuard],");
                s.Add($"        component: {entity.Name}ListComponent,");
                s.Add($"        data: {{");
                if (!string.IsNullOrWhiteSpace(entity.Menu))
                    s.Add($"            menu: '{entity.Menu}',");
                s.Add($"            breadcrumb: '{entity.PluralFriendlyName}'");
                s.Add($"        }}" + (editOnRoot ? "," : ""));
                if (editOnRoot)
                {
                    s.Add($"        children: [");
                    if (editOnRoot)
                    {
                        s.Add($"            {{");
                        s.Add($"                path: '{entity.KeyFields.Select(o => ":" + o.Name.ToCamelCase()).Aggregate((current, next) => { return current + "/" + next; })}',");
                        s.Add($"                component: {entity.Name}EditComponent,");
                        s.Add($"                canActivate: [AccessGuard],");
                        s.Add($"                canActivateChild: [AccessGuard],");
                        s.Add($"                data: {{");
                        s.Add($"                    menu: '{entity.Menu}',");
                        s.Add($"                    breadcrumb: 'Add {entity.FriendlyName}'");
                        s.Add($"                }}" + (childRelationships.Any() ? "," : ""));
                    }
                    WriteChildRoutes(childRelationships, s, 0);
                    s.Add($"            }}");
                    s.Add($"        ]");
                }
                s.Add($"    }}" + (entity == allEntities.Last() ? "" : ","));
            }

            s.Add($"];");

            return RunCodeReplacements(s.ToString(), CodeType.AppRouter);

        }
    }
}