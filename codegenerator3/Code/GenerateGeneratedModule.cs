using System.Linq;
using System.Text;

namespace WEB.Models
{
    public partial class Code
    {
        public string GenerateGeneratedModule()
        {
            var s = new StringBuilder();

            s.Add($"import {{ NgModule }} from '@angular/core';");
            s.Add($"import {{ CommonModule }} from '@angular/common';");
            s.Add($"import {{ FormsModule }} from '@angular/forms';");
            s.Add($"import {{ RouterModule }} from '@angular/router';");
            s.Add($"import {{ NgbModule }} from '@ng-bootstrap/ng-bootstrap';");
            s.Add($"import {{ DragDropModule }} from '@angular/cdk/drag-drop';");

            var entitiesToBundle = AllEntities.Where(e => !e.Exclude);
            foreach (var e in entitiesToBundle)
            {
                s.Add($"import {{ {e.Name}ListComponent }} from './{e.Project.GeneratedPath ?? string.Empty}{e.PluralName.ToLower()}/{e.Name.ToLower()}.list.component';");
                s.Add($"import {{ {e.Name}EditComponent }} from './{e.Project.GeneratedPath ?? string.Empty}{e.PluralName.ToLower()}/{e.Name.ToLower()}.edit.component';");
            }
            s.Add($"import {{ SharedModule }} from './shared.module';");
            s.Add($"import {{ GeneratedRoutes }} from './generated.routes';");
            s.Add($"");


            s.Add($"@NgModule({{");
            s.Add($"    declarations: [");
            foreach (var e in entitiesToBundle)
            {
                s.Add($"        {e.Name}ListComponent,");
                s.Add($"        {e.Name}EditComponent{(e == entitiesToBundle.Last() ? "" : ",")}");
            }
            s.Add($"    ],");
            s.Add($"    imports: [");
            s.Add($"        CommonModule,");
            s.Add($"        FormsModule,");
            s.Add($"        RouterModule.forChild(GeneratedRoutes),");
            s.Add($"        NgbModule,");
            s.Add($"        DragDropModule,");
            s.Add($"        SharedModule");
            s.Add($"    ]");
            s.Add($"}})");
            s.Add($"export class GeneratedModule {{ }}");

            return RunCodeReplacements(s.ToString(), CodeType.GeneratedModule);

        }
    }
}