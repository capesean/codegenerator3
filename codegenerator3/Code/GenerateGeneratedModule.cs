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
            var componentList = "";
            foreach (var e in entitiesToBundle)
            {
                s.Add($"import {{ {e.Name}ListComponent }} from './{e.PluralName.ToLower()}/{e.Name.ToLower()}.list.component';");
                s.Add($"import {{ {e.Name}EditComponent }} from './{e.PluralName.ToLower()}/{e.Name.ToLower()}.edit.component';");
                componentList += (componentList == "" ? "" : ", ") + $"{e.Name}ListComponent, {e.Name}EditComponent";

                //if (string.IsNullOrWhiteSpace(e.PreventAppSelectTypeScriptDeployment))
                //{
                //    s.Add($"import {{ {e.Name}SelectComponent }} from './{e.PluralName.ToLower()}/{e.Name.ToLower()}.select.component';");
                //    componentList += (componentList == "" ? "" : ", ") + $"{e.Name}SelectComponent";
                //}

                //if (string.IsNullOrWhiteSpace(e.PreventSelectModalTypeScriptDeployment))
                //{
                //    s.Add($"import {{ {e.Name}ModalComponent }} from './{e.PluralName.ToLower()}/{e.Name.ToLower()}.modal.component';");
                //    componentList += (componentList == "" ? "" : ", ") + $"{e.Name}ModalComponent";
                //}
            }
            s.Add($"import {{ SharedModule }} from './shared.module';");
            s.Add($"import {{ GeneratedRoutes }} from './generated.routes';");
            s.Add($"");


            s.Add($"@NgModule({{");
            s.Add($"   declarations: [{componentList}],");
            s.Add($"   imports: [");
            s.Add($"      CommonModule,");
            s.Add($"      FormsModule,");
            s.Add($"      RouterModule.forChild(GeneratedRoutes),");
            s.Add($"      NgbModule,");
            s.Add($"      DragDropModule,");
            s.Add($"      SharedModule");
            s.Add($"   ]");
            s.Add($"}})");
            s.Add($"export class GeneratedModule {{ }}");

            return RunCodeReplacements(s.ToString(), CodeType.GeneratedModule);

        }
    }
}