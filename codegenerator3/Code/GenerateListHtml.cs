﻿using System.Linq;
using System.Text;
using WEB.Migrations;

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
                s.Add($"<ng-container *ngIf=\"route.children.length === 0\">");
                s.Add($"");
                t = "    ";
            }
            s.Add(t + $"<app-page-title></app-page-title>");
            s.Add($"");
            s.Add(t + $"<div class=\"card card-list\">");
            s.Add($"");
            s.Add(t + $"    <div class=\"card-header\">");
            s.Add($"");
            s.Add(t + $"        <div class=\"card-header-title\">");
            s.Add(t + $"            <h4>{CurrentEntity.PluralFriendlyName}</h4>");

            var hasIcons = CurrentEntity.RelationshipsAsChild.Count(r => r.Hierarchy) == 0
                || (CurrentEntity.Fields.Any(f => f.SearchType != SearchType.None) || CurrentEntity.EntityType == EntityType.User)
                || CurrentEntity.HasASortField;

            if (hasIcons)
                s.Add(t + $"            <div>");

            if (CurrentEntity.RelationshipsAsChild.Count(r => r.Hierarchy) == 0)
                s.Add(t + $"                <i class=\"fa fa-fw ms-1 fa-plus cursor-pointer\" ngbTooltip=\"Add {CurrentEntity.FriendlyName}\" [routerLink]=\"['./', 'add']\"></i>");

            if (CurrentEntity.Fields.Any(f => f.SearchType != SearchType.None) || CurrentEntity.EntityType == EntityType.User)
                s.Add(t + $"                <i class=\"fa fa-fw ms-1 fa-search cursor-pointer\" ngbTooltip=\"Toggle search options\" (click)=\"showSearchOptions = !showSearchOptions\"></i>");

            if (CurrentEntity.HasASortField)
                s.Add(t + $"                <i class=\"fa fa-fw ms-1 fa-sort cursor-pointer\" ngbTooltip=\"Sort {CurrentEntity.PluralFriendlyName}\" (click)=\"showSort()\"></i>");

            if (hasIcons)
                s.Add(t + $"            </div>");

            s.Add(t + $"        </div>");
            s.Add($"");
            s.Add(t + $"    </div>");
            s.Add($"");
            s.Add(t + $"    <div class=\"card-body\" *ngIf=\"showSearchOptions\" @FadeThenShrink>");
            s.Add($"");
            if (CurrentEntity.Fields.Any(f => f.SearchType != SearchType.None) || CurrentEntity.EntityType == EntityType.User)
            {
                s.Add(t + $"        <form (submit)=\"runSearch(0)\" novalidate>");
                s.Add($"");
                s.Add(t + $"            <div class=\"row g-2\">");
                s.Add($"");

                if (CurrentEntity.Fields.Any(f => f.SearchType == SearchType.Text))
                {
                    s.Add(t + $"                <div class=\"col-sm-6 col-md-4 col-lg-3\">");
                    s.Add(t + $"                    <div class=\"form-group\">");
                    s.Add(t + $"                        <input type=\"search\" name=\"q\" id=\"q\" [(ngModel)]=\"searchOptions.q\" max=\"100\" class=\"form-control\" placeholder=\"Search {CurrentEntity.PluralFriendlyName.ToLower()}\" />");
                    s.Add(t + $"                    </div>");
                    s.Add(t + $"                </div>");
                    s.Add($"");
                }

                if (CurrentEntity.EntityType == EntityType.User)
                {
                    s.Add(t + $"                <div class=\"col-sm-6 col-md-4 col-lg-3\">");
                    s.Add(t + $"                    <div class=\"form-group\">");
                    s.Add(t + $"                        <select id=\"roles\" name=\"roles\" class=\"form-select\" [(ngModel)]=\"searchOptions.roleName\">");
                    s.Add(t + $"                            <option [value]=\"undefined\">All roles</option>");
                    s.Add(t + $"                            <option *ngFor=\"let role of roles\" [ngValue]=\"role.name\">{{{{role.label}}}}</option>");
                    s.Add(t + $"                        </select>");
                    s.Add(t + $"                    </div>");
                    s.Add(t + $"                </div>");
                    s.Add($"");
                }

                foreach (var field in CurrentEntity.Fields.Where(f => f.SearchType == SearchType.Exact).OrderBy(f => f.FieldOrder))
                {
                    if (field.CustomType == CustomType.Enum)
                    {
                        s.Add(t + $"                <div class=\"col-sm-6 col-md-4 col-lg-3\">");
                        s.Add(t + $"                    <div class=\"form-group\">");
                        s.Add(t + $"                        <select id=\"{field.Name.ToCamelCase()}\" name=\"{field.Name.ToCamelCase()}\" [(ngModel)]=\"searchOptions.{field.Name.ToCamelCase()}\" #{field.Name.ToCamelCase()}=\"ngModel\" class=\"form-select\">");
                        s.Add(t + $"                            <option [ngValue]=\"undefined\" disabled>{field.Label}</option>");
                        s.Add(t + $"                            <option *ngFor=\"let {field.Lookup.Name.ToCamelCase()} of {field.Lookup.PluralName.ToCamelCase()}\" [ngValue]=\"{field.Lookup.Name.ToCamelCase()}.value\">{{{{ {field.Lookup.Name.ToCamelCase()}.label }}}}</option>");
                        s.Add(t + $"                        </select>");
                        s.Add(t + $"                    </div>");
                        s.Add(t + $"                </div>");
                        s.Add($"");
                    }
                    else if (CurrentEntity.RelationshipsAsChild.Any(r => r.RelationshipFields.Any(f => f.ChildFieldId == field.FieldId)))
                    {
                        var relationship = CurrentEntity.GetParentSearchRelationship(field);
                        var parentEntity = relationship.ParentEntity;
                        var relField = relationship.RelationshipFields.Single();
                        s.Add(t + $"                <div class=\"col-sm-6 col-md-5 col-xl-4\">");
                        s.Add(t + $"                    <div class=\"form-group\">");
                        s.Add(t + $"                        {relationship.AppSelector}");
                        s.Add(t + $"                    </div>");
                        s.Add(t + $"                </div>");
                        s.Add($"");
                    }
                    else if (field.CustomType == CustomType.Boolean)
                    {
                        s.Add(t + $"                <div class=\"col-sm-6 col-md-4 col-xl-3\">");
                        s.Add(t + $"                    <div class=\"form-group\">");
                        s.Add(t + $"                        <select id=\"{field.Name.ToCamelCase()}\" name=\"{field.Name.ToCamelCase()}\" [(ngModel)]=\"searchOptions.{field.Name.ToCamelCase()}\" #{field.Name.ToCamelCase()}=\"ngModel\" class=\"form-select\">");
                        s.Add(t + $"                            <option [ngValue]=\"undefined\">{field.Label}: Any</option>");
                        s.Add(t + $"                            <option [ngValue]=\"true\">{field.Label}: Yes</option>");
                        s.Add(t + $"                            <option [ngValue]=\"false\">{field.Label}: No</option>");
                        s.Add(t + $"                        </select>");
                        s.Add(t + $"                    </div>");
                        s.Add(t + $"                </div>");
                        s.Add($"");
                    }
                }

                foreach (var field in CurrentEntity.Fields.Where(f => f.SearchType == SearchType.Range))
                {
                    if (field.CustomType == CustomType.Date)
                    {
                        s.Add(t + $"                <div class=\"col-sm-6 col-md-4 col-xl-3\">");
                        s.Add(t + $"                    <div class=\"form-group\" ngbTooltip=\"From Date\" container=\"body\" placement=\"top\">");
                        s.Add(t + $"                        <div class=\"input-group\">");
                        s.Add(t + $"                            <input type=\"text\" id=\"from{field.Name}\" name=\"from{field.Name}\" [(ngModel)]=\"searchOptions.from{field.Name}\" #from{field.Name}=\"ngModel\" class=\"form-control\" readonly placeholder=\"yyyy-mm-dd\" ngbDatepicker #dpFrom{field.Name}=\"ngbDatepicker\" tabindex=\"-1\" (click)=\"dpFrom{field.Name}.toggle()\" container=\"body\" />");
                        s.Add(t + $"                            <button class=\"btn btn-secondary calendar\" (click)=\"dpFrom{field.Name}.toggle()\" type=\"button\"><i class=\"fas fa-fw fa-calendar-alt\"></i></button>");
                        s.Add(t + $"                        </div>");
                        s.Add(t + $"                    </div>");
                        s.Add(t + $"                </div>");
                        s.Add($"");
                        s.Add(t + $"                <div class=\"col-sm-6 col-md-4 col-xl-3\">");
                        s.Add(t + $"                    <div class=\"form-group\" ngbTooltip=\"To Date\" container=\"body\" placement=\"top\">");
                        s.Add(t + $"                        <div class=\"input-group\">");
                        s.Add(t + $"                            <input type=\"text\" id=\"to{field.Name}\" name=\"to{field.Name}\" [(ngModel)]=\"searchOptions.to{field.Name}\" #to{field.Name}=\"ngModel\" class=\"form-control\" readonly placeholder=\"yyyy-mm-dd\" ngbDatepicker #dpTo{field.Name}=\"ngbDatepicker\" tabindex=\"-1\" (click)=\"dpTo{field.Name}.toggle()\" container=\"body\" />");
                        s.Add(t + $"                            <button class=\"btn btn-secondary calendar\" (click)=\"dpTo{field.Name}.toggle()\" type=\"button\"><i class=\"fas fa-fw fa-calendar-alt\"></i></button>");
                        s.Add(t + $"                        </div>");
                        s.Add(t + $"                    </div>");
                        s.Add(t + $"                </div>");
                        s.Add($"");
                    }
                }

                s.Add(t + $"                <div class=\"col-sm-3 col-md-3 col-lg-3 col-xl-2\">");
                s.Add(t + $"                    <div class=\"form-group\">");
                s.Add(t + $"                        <button type=\"submit\" class=\"btn btn-outline-primary me-2 mb-1\">Search<i class=\"fas fa-search ms-2\"></i></button>");
                s.Add(t + $"                    </div>");
                s.Add(t + $"                </div>");
                s.Add($"");

                s.Add(t + $"            </div>");
                s.Add($"");
                s.Add(t + $"        </form>");
            }
            s.Add($"");
            s.Add(t + $"    </div>");
            s.Add($"");

            s.Add(t + $"    <div class=\"table-responsive\">");
            s.Add($"");

            s.Add(t + $"        <table class=\"table table-hover table-striped table-nowrap mb-0 align-middle\">");
            s.Add(t + $"            <thead class=\"thead-light\">");
            s.Add(t + $"                <tr>");
            foreach (var field in CurrentEntity.Fields.Where(f => f.ShowInSearchResults).OrderBy(f => f.FieldOrder))
                if (field.Sortable)
                {
                    s.Add(t + $"                    <th class='cursor-pointer' (click)=\"runSearch(headers.pageIndex, '{field.Name.ToLower()}')\">{field.Label}<app-sort-icon name=\"{field.Name.ToLower()}\" [searchOptions]=\"searchOptions\"></app-sort-icon></th>");
                }
                else
                {
                    s.Add(t + $"                    <th{(string.IsNullOrWhiteSpace(field.DisplayClasses) ? "" : $" class=\"{field.DisplayClasses}\"")}>{field.Label}</th>");
                }
            if (CurrentEntity.HasAFileContentsField)
                s.Add(t + $"                    <th class=\"w-20px text-center\"><i class=\"fas fa-cloud-download text-secondary\"></i></th>");
            s.Add(t + $"                </tr>");
            s.Add(t + $"            </thead>");
            s.Add(t + $"            <tbody class=\"list cursor-pointer\">");
            s.Add(t + $"                <tr *ngFor=\"let {CurrentEntity.CamelCaseName} of {CurrentEntity.PluralName.ToCamelCase()}\" (click)=\"goTo{CurrentEntity.Name}({CurrentEntity.CamelCaseName})\">");
            foreach (var field in CurrentEntity.Fields.Where(f => f.ShowInSearchResults).OrderBy(f => f.FieldOrder))
            {
                s.Add(t + $"                    <td{(string.IsNullOrWhiteSpace(field.DisplayClasses) ? "" : $" class=\"{field.DisplayClasses}\"")}>{field.ListFieldHtml}</td>");
            }
            if (CurrentEntity.HasAFileContentsField)
                s.Add(t + $"                    <td class=\"text-center\"><i class=\"fas fa-cloud-download cursor-pointer p-1 text-primary\" (click)=\"download{CurrentEntity.Name}({CurrentEntity.Name.ToCamelCase()}, $event)\"></i></td>");
            s.Add(t + $"                </tr>");
            s.Add(t + $"            </tbody>");
            s.Add(t + $"        </table>");
            s.Add($"");

            s.Add(t + $"    </div>");
            s.Add($"");

            s.Add(t + $"    <div class=\"card-footer\">");
            s.Add(t + $"        <pager [headers]=\"headers\" (pageChanged)=\"runSearch($event)\"></pager>");
            s.Add(t + $"    </div>");
            s.Add($"");

            s.Add(t + $"</div>");
            s.Add($"");


            if (hasChildRoutes)
            {
                s.Add($"</ng-container>");
                s.Add($"");
                s.Add($"<router-outlet></router-outlet>");
            }

            return RunCodeReplacements(s.ToString(), CodeType.ListHtml);
        }
    }
}