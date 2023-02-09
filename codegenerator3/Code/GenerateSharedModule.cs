using System;
using System.IO;
using System.Linq;
using System.Text;

namespace WEB.Models
{
    public partial class Code
    {
        public string GenerateSharedModule()
        {
            var folderCount = string.IsNullOrWhiteSpace(CurrentEntity.Project.GeneratedPath) ? 0 : 1;
            if (folderCount == 1 && CurrentEntity.Project.GeneratedPath.Contains("/")) folderCount += CurrentEntity.Project.GeneratedPath.Count(o => o == '/');
            var folders = string.Join("", Enumerable.Repeat("../", folderCount));

            var s = new StringBuilder();

            var file = File.ReadAllText(AppDomain.CurrentDomain.BaseDirectory + "templates/shared.module.txt");

            var entities = AllEntities.Where(e => !e.Exclude);

            var component = string.Empty;
            var imports = string.Empty;

            foreach (var e in entities)
            {
                if (string.IsNullOrWhiteSpace(e.PreventAppSelectTypeScriptDeployment))
                {
                    imports += $"import {{ {e.Name}SelectComponent }} from './{e.Project.GeneratedPath}{e.PluralName.ToLower()}/{e.Name.ToLower()}.select.component';{Environment.NewLine}";
                    component += $",{Environment.NewLine}        {e.Name}SelectComponent";
                }

                if (string.IsNullOrWhiteSpace(e.PreventSelectModalTypeScriptDeployment))
                {
                    imports += $"import {{ {e.Name}ModalComponent }} from './{e.Project.GeneratedPath}{e.PluralName.ToLower()}/{e.Name.ToLower()}.modal.component';{Environment.NewLine}";
                    component += $",{Environment.NewLine}        {e.Name}ModalComponent";
                }

                if (e.HasASortField)
                {
                    imports += $"import {{ {e.Name}SortComponent }} from './{e.Project.GeneratedPath}{e.PluralName.ToLower()}/{e.Name.ToLower()}.sort.component';{Environment.NewLine}";
                    component += $",{Environment.NewLine}        {e.Name}SortComponent";
                }
            }

            s.Add(RunTemplateReplacements(file)
                .Replace("/*IMPORTS*/", imports)
                .Replace("/*COMPONENTS*/", component)
                );

            return RunCodeReplacements(s.ToString(), CodeType.SharedModule);

        }
    }
}