﻿using System.Linq;
using System.Text;

namespace WEB.Models
{
    public partial class Code
    {
        public string GenerateListHtml()
        {
            var relationshipsAsParent = CurrentEntity.RelationshipsAsParent.Where(r => !r.ChildEntity.Exclude && r.DisplayListOnParent && r.Hierarchy).OrderBy(r => r.SortOrder);
            var hasChildRoutes = relationshipsAsParent.Any();
            if (!CurrentEntity.RelationshipsAsChild.Any(o => o.Hierarchy && !o.ParentEntity.Exclude))
                hasChildRoutes = true;

            var s = new StringBuilder();
            var t = "";
            if (hasChildRoutes)
            {
                s.Add($"<div *ngIf=\"route.children.length === 0\">");
                s.Add($"");
                t = "    ";
            }
            if (CurrentEntity.Fields.Any(f => f.SearchType != SearchType.None) || CurrentEntity.EntityType == EntityType.User)
            {
                s.Add(t + $"<form (submit)=\"runSearch(0)\" novalidate>");
                s.Add($"");
                s.Add(t + $"    <div class=\"row g-3\">");
                s.Add($"");

                if (CurrentEntity.Fields.Any(f => f.SearchType == SearchType.Text))
                {
                    s.Add(t + $"        <div class=\"col-sm-6 col-md-4 col-lg-3\">");
                    s.Add(t + $"            <div class=\"form-group\">");
                    s.Add(t + $"                <input type=\"search\" name=\"q\" id=\"q\" [(ngModel)]=\"searchOptions.q\" max=\"100\" class=\"form-control\" placeholder=\"Search {CurrentEntity.PluralFriendlyName.ToLower()}\" />");
                    s.Add(t + $"            </div>");
                    s.Add(t + $"        </div>");
                    s.Add($"");
                }

                if (CurrentEntity.EntityType == EntityType.User)
                {
                    s.Add(t + $"        <div class=\"col-sm-6 col-md-4 col-lg-3\">");
                    s.Add(t + $"            <div class=\"form-group\">");
                    s.Add(t + $"                <select id=\"roles\" name=\"roles\" class=\"form-select\" [(ngModel)]=\"searchOptions.roleName\">");
                    s.Add(t + $"                    <option [value]=\"undefined\">All roles</option>");
                    s.Add(t + $"                    <option *ngFor=\"let role of roles\" [ngValue]=\"role.name\">{{{{role.name}}}}</option>");
                    s.Add(t + $"                </select>");
                    s.Add(t + $"            </div>");
                    s.Add(t + $"        </div>");
                    s.Add($"");
                }

                foreach (var field in CurrentEntity.Fields.Where(f => f.SearchType == SearchType.Exact).OrderBy(f => f.FieldOrder))
                {
                    if (field.CustomType == CustomType.Enum)
                    {
                        s.Add(t + $"        <div class=\"col-sm-6 col-md-4 col-lg-3\">");
                        s.Add(t + $"            <div class=\"form-group\">");
                        s.Add(t + $"                <select id=\"{field.Name.ToCamelCase()}\" name=\"{field.Name.ToCamelCase()}\" [(ngModel)]=\"searchOptions.{field.Name.ToCamelCase()}\" #{field.Name.ToCamelCase()}=\"ngModel\" class=\"form-select\">");
                        s.Add(t + $"                    <option *ngFor=\"let {field.Lookup.Name.ToCamelCase()} of {field.Lookup.PluralName.ToCamelCase()}\" [ngValue]=\"{field.Lookup.Name.ToCamelCase()}.value\">{{{{ {field.Lookup.Name.ToCamelCase()}.label }}}}</option>");
                        s.Add(t + $"                </select>");
                        s.Add(t + $"            </div>");
                        s.Add(t + $"        </div>");
                        s.Add($"");
                    }
                    else if (CurrentEntity.RelationshipsAsChild.Any(r => r.RelationshipFields.Any(f => f.ChildFieldId == field.FieldId)))
                    {
                        var relationship = CurrentEntity.GetParentSearchRelationship(field);
                        var parentEntity = relationship.ParentEntity;
                        var relField = relationship.RelationshipFields.Single();
                        if (true || relationship.UseSelectorDirective)
                        {
                            s.Add(t + $"        <div class=\"col-sm-6 col-md-4 col-lg-3\">");
                            s.Add(t + $"            <div class=\"form-group\">");
                            s.Add(t + $"                {relationship.AppSelector}");
                            s.Add(t + $"            </div>");
                            s.Add(t + $"        </div>");
                            s.Add($"");
                        }
                        else
                        {
                            //s.Add(t + $"        <div class=\"col-sm-6 col-md-4 col-lg-3\">");
                            //s.Add(t + $"            <div class=\"form-group\">");
                            //s.Add(t + $"                <ol id=\"{field.Name.ToCamelCase()}\" name=\"{field.Name.ToCamelCase()}\" title=\"{parentEntity.PluralFriendlyName}\" class=\"nya-bs-select form-control\" [(ngModel)]=\"searchOptions.{field.Name.ToCamelCase()}\" data-live-search=\"true\" data-size=\"10\">");
                            //s.Add(t + $"                    <li nya-bs-option=\"{parentEntity.Name.ToCamelCase()} in vm.{parentEntity.PluralName.ToCamelCase()}\" class=\"nya-bs-option{(CurrentEntity.Project.Bootstrap3 ? "" : " dropdown-item")}\" data-value=\"{parentEntity.Name.ToCamelCase()}.{relField.ParentField.Name.ToCamelCase()}\">");
                            //s.Add(t + $"                        <a>{{{{{parentEntity.Name.ToCamelCase()}.{relationship.ParentField.Name.ToCamelCase()}}}}}<span class=\"fas fa-check check-mark\"></span></a>");
                            //s.Add(t + $"                    </li>");
                            //s.Add(t + $"                </ol>");
                            //s.Add(t + $"            </div>");
                            //s.Add(t + $"        </div>");
                            //s.Add($"");
                        }
                    }
                }
                foreach (var field in CurrentEntity.Fields.Where(f => f.SearchType == SearchType.Range))
                {
                    if (field.CustomType == CustomType.Date)
                    {
                        s.Add(t + $"        <div class=\"col-sm-6 col-md-3 col-lg-2\">");
                        s.Add(t + $"            <div class=\"form-group\" ngbTooltip=\"From Date\" container=\"body\" placement=\"top\">");
                        s.Add(t + $"                <div class=\"input-group\">");
                        s.Add(t + $"                    <input type=\"text\" id=\"from{field.Name}\" name=\"from{field.Name}\" [(ngModel)]=\"searchOptions.from{field.Name}\" #from{field.Name}=\"ngModel\" class=\"form-control\" readonly placeholder=\"yyyy-mm-dd\" ngbDatepicker #dpFrom{field.Name}=\"ngbDatepicker\" tabindex=\"-1\" (click)=\"dpFrom{field.Name}.toggle()\" container=\"body\" />");
                        s.Add(t + $"                    <button class=\"btn btn-secondary calendar\" (click)=\"dpFrom{field.Name}.toggle()\" type=\"button\"><i class=\"fas fa-calendar-alt\"></i></button>");
                        s.Add(t + $"                </div>");
                        s.Add(t + $"            </div>");
                        s.Add(t + $"        </div>");
                        s.Add($"");
                        s.Add(t + $"        <div class=\"col-sm-6 col-md-3 col-lg-2\">");
                        s.Add(t + $"            <div class=\"form-group\" ngbTooltip=\"To Date\" container=\"body\" placement=\"top\">");
                        s.Add(t + $"                <div class=\"input-group\">");
                        s.Add(t + $"                    <input type=\"text\" id=\"to{field.Name}\" name=\"to{field.Name}\" [(ngModel)]=\"searchOptions.to{field.Name}\" #to{field.Name}=\"ngModel\" class=\"form-control\" readonly placeholder=\"yyyy-mm-dd\" ngbDatepicker #dpTo{field.Name}=\"ngbDatepicker\" tabindex=\"-1\" (click)=\"dpTo{field.Name}.toggle()\" container=\"body\" />");
                        s.Add(t + $"                    <button class=\"btn btn-secondary calendar\" (click)=\"dpTo{field.Name}.toggle()\" type=\"button\"><i class=\"fas fa-calendar-alt\"></i></button>");
                        s.Add(t + $"                </div>");
                        s.Add(t + $"            </div>");
                        s.Add(t + $"        </div>");
                        s.Add($"");
                    }
                }
                s.Add(t + $"    </div>");
                s.Add($"");
                s.Add(t + $"    <fieldset class=\"my-3\">");
                s.Add($"");
                s.Add(t + $"        <button type=\"submit\" class=\"btn btn-success me-2 mb-1\">Search<i class=\"fas fa-search ms-1\"></i></button>");
                if (CurrentEntity.RelationshipsAsChild.Count(r => r.Hierarchy) == 0)
                {
                    s.Add(t + $"        <a [routerLink]=\"['./', 'add']\" class=\"btn btn-primary me-2 mb-1\">Add<i class=\"fas fa-plus-circle ms-1\"></i></a>");
                }
                if (CurrentEntity.HasASortField)
                    s.Add(t + $"        <button type=\"button\" class=\"btn btn-outline-secondary me-2 mb-1\" (click)=\"showSort()\" *ngIf=\"headers.totalRecords > 1\">Sort<i class=\"fas fa-sort ms-1\"></i></button>");
                s.Add($"");
                s.Add(t + $"    </fieldset>");
                s.Add($"");
                s.Add(t + $"</form>");
            }
            s.Add($"");
            s.Add(t + $"<hr />");
            s.Add($"");

            s.Add(t + $"<table class=\"table table-striped table-hover table-sm row-navigation\">");
            s.Add(t + $"    <thead>");
            s.Add(t + $"        <tr>");
            foreach (var field in CurrentEntity.Fields.Where(f => f.ShowInSearchResults).OrderBy(f => f.FieldOrder))
                s.Add(t + $"            <th>{field.Label}</th>");
            s.Add(t + $"        </tr>");
            s.Add(t + $"    </thead>");
            s.Add(t + $"    <tbody>");
            s.Add(t + $"        <tr *ngFor=\"let {CurrentEntity.CamelCaseName} of {CurrentEntity.PluralName.ToCamelCase()}\" (click)=\"goTo{CurrentEntity.Name}({CurrentEntity.CamelCaseName})\">");
            foreach (var field in CurrentEntity.Fields.Where(f => f.ShowInSearchResults).OrderBy(f => f.FieldOrder))
            {
                s.Add(t + $"            <td>{field.ListFieldHtml}</td>");
            }
            s.Add(t + $"        </tr>");
            s.Add(t + $"    </tbody>");
            s.Add(t + $"</table>");
            s.Add($"");
            s.Add(t + $"<pager [headers]=\"headers\" (pageChanged)=\"runSearch($event)\"></pager>");
            s.Add($"");
            if (hasChildRoutes)
            {
                s.Add($"</div>");
                s.Add($"");
                s.Add($"<router-outlet></router-outlet>");
            }

            return RunCodeReplacements(s.ToString(), CodeType.ListHtml);
        }
    }
}