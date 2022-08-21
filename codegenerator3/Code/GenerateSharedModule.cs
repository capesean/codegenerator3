using System;
using System.Linq;
using System.Text;

namespace WEB.Models
{
    public partial class Code
    {
        public string GenerateSharedModule()
        {
            var s = new StringBuilder();

            s.Add($"import {{ NgModule }} from '@angular/core';");
            s.Add($"import {{ CommonModule }} from '@angular/common';");
            s.Add($"import {{ PagerComponent }} from './common/components/pager.component';");
            s.Add($"import {{ RouterModule }} from '@angular/router';");
            s.Add($"import {{ MainComponent }} from './main.component';");
            s.Add($"import {{ NavMenuComponent }} from './common/nav-menu/nav-menu.component';");
            s.Add($"import {{ MomentPipe }} from './common/pipes/momentPipe';");
            s.Add($"import {{ BooleanPipe }} from './common/pipes/booleanPipe';");
            s.Add($"import {{ ConfirmModalComponent }} from './common/components/confirm.component';");
            s.Add($"import {{ FormsModule }} from '@angular/forms';");
            s.Add($"import {{ NgbModule }} from '@ng-bootstrap/ng-bootstrap';");
            s.Add($"import {{ DragDropModule }} from '@angular/cdk/drag-drop';");
            s.Add($"import {{ BreadcrumbModule }} from 'primeng/breadcrumb';");
            s.Add($"import {{ AppFileInputDirective }} from './common/directives/appfileinput';");
            s.Add($"import {{ FileComponent }} from './common/components/file.component';");
            s.Add($"import {{ AppHasRoleDirective }} from './common/directives/apphasrole';");

            var entitiesToBundle = AllEntities.Where(e => !e.Exclude);
            var componentList = "";
            foreach (var e in entitiesToBundle)
            {
                if (string.IsNullOrWhiteSpace(e.PreventAppSelectTypeScriptDeployment))
                {
                    s.Add($"import {{ {e.Name}SelectComponent }} from './{e.PluralName.ToLower()}/{e.Name.ToLower()}.select.component';");
                    componentList += "," + Environment.NewLine + $"        {e.Name}SelectComponent";
                }

                if (string.IsNullOrWhiteSpace(e.PreventSelectModalTypeScriptDeployment))
                {
                    s.Add($"import {{ {e.Name}ModalComponent }} from './{e.PluralName.ToLower()}/{e.Name.ToLower()}.modal.component';");
                    componentList += "," + Environment.NewLine + $"        {e.Name}ModalComponent";
                }

                if (e.HasASortField)
                {
                    s.Add($"import {{ {e.Name}SortComponent }} from './{e.PluralName.ToLower()}/{e.Name.ToLower()}.sort.component';");
                    componentList += "," + Environment.NewLine + $"        {e.Name}SortComponent";
                }
            }
            s.Add($"");


            s.Add($"@NgModule({{");
            //s.Add($"   declarations: [{componentList}],");
            s.Add($"    imports: [");
            s.Add($"        CommonModule,");
            s.Add($"        FormsModule,");
            s.Add($"        RouterModule,");
            s.Add($"        NgbModule,");
            s.Add($"        DragDropModule,");
            s.Add($"        BreadcrumbModule");
            s.Add($"    ],");
            s.Add($"    declarations: [");
            s.Add($"        PagerComponent,");
            s.Add($"        MainComponent,");
            s.Add($"        NavMenuComponent,");
            s.Add($"        MomentPipe,");
            s.Add($"        BooleanPipe,");
            s.Add($"        ConfirmModalComponent,");
            s.Add($"        AppFileInputDirective,");
            s.Add($"        FileComponent,");
            s.Add($"        AppHasRoleDirective" + componentList);
            s.Add($"    ],");
            s.Add($"    exports: [");
            s.Add($"        PagerComponent,");
            s.Add($"        MainComponent,");
            s.Add($"        NavMenuComponent,");
            s.Add($"        NgbModule,");
            s.Add($"        MomentPipe,");
            s.Add($"        BooleanPipe,");
            s.Add($"        ConfirmModalComponent,");
            s.Add($"        AppFileInputDirective,");
            s.Add($"        FileComponent,");
            s.Add($"        AppHasRoleDirective" + componentList);
            s.Add($"    ]");
            s.Add($"}})");
            s.Add($"export class SharedModule {{ }}");

            return RunCodeReplacements(s.ToString(), CodeType.SharedModule);

        }
    }
}