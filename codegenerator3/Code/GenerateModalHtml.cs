using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Data.Entity;
using System.IO;

namespace WEB.Models
{
    public partial class Code
    {
        public string GenerateModalHtml()
        {
            var s = new StringBuilder();

            var file = File.ReadAllText(AppDomain.CurrentDomain.BaseDirectory + "templates/selectmodal.html");

            var fieldHeaders = string.Empty;
            var fieldList = string.Empty;
            var appSelectFilters = string.Empty;
            var filterAlerts = string.Empty;
            var appTextFilter = string.Empty;

            foreach (var field in CurrentEntity.Fields.Where(f => f.ShowInSearchResults).OrderBy(f => f.FieldOrder))
            {
                var ngIf = string.Empty;
                if (CurrentEntity.Fields.Any(o => o.FieldId == field.FieldId && o.SearchType == SearchType.Exact))
                {
                    if (field.FieldType == FieldType.Enum)
                        ngIf = " *ngIf=\"!" + field.Name.ToCamelCase() + $"\"";
                    else
                    {
                        if (CurrentEntity.RelationshipsAsChild.Any(r => r.RelationshipFields.Any(rf => rf.ChildFieldId == field.FieldId) && r.UseSelectorDirective))
                        {
                            var relationship = CurrentEntity.GetParentSearchRelationship(field);
                            ngIf = " *ngIf=\"!" + relationship.ParentName.ToCamelCase().ToCamelCase() + $"\"";
                        }
                    }
                }

                fieldHeaders += (fieldHeaders == string.Empty ? string.Empty : Environment.NewLine) + $"                        <th{ngIf}>{field.Label}</th>";
                fieldList += (fieldList == string.Empty ? string.Empty : Environment.NewLine);

                fieldList += $"                        <td{ngIf}>{field.ListFieldHtml}</td>";
            }

            if (CurrentEntity.Fields.Any(o => o.SearchType == SearchType.Text))
            {
                appTextFilter += $"                    <div class=\"col-sm-6 col-md-6 col-lg-4\">" + Environment.NewLine;
                appTextFilter += $"                        <div class=\"form-group\">" + Environment.NewLine;
                appTextFilter += $"                            <input type=\"search\" ngbAutofocus name=\"q\" id=\"q\" [(ngModel)]=\"searchOptions.q\" max=\"100\" class=\"form-control\" placeholder=\"Search PLURALFRIENDLYNAME_TOLOWER\" autocomplete=\"off\" />" + Environment.NewLine;
                appTextFilter += $"                        </div>" + Environment.NewLine;
                appTextFilter += $"                    </div>" + Environment.NewLine;
                appTextFilter += Environment.NewLine;
            }

            if (CurrentEntity.EntityType == EntityType.User)
                filterAlerts += Environment.NewLine + $"                <div class=\"alert alert-info alert-dismissible\" *ngIf=\"role!=undefined\">Filtered by role: {{{{role.label}}}}<button type=\"button\" class=\"btn-close\" data-bs-dismiss=\"alert\" (click)=\"searchOptions.roleName=undefined;role=undefined;runSearch();\" *ngIf=\"canRemoveFilters\"></button></div>" + Environment.NewLine;

            foreach (var field in CurrentEntity.Fields.Where(f => f.SearchType == SearchType.Exact).OrderBy(f => f.FieldOrder))
            {
                Relationship relationship = null;
                if (CurrentEntity.RelationshipsAsChild.Any(r => r.RelationshipFields.Any(rf => rf.ChildFieldId == field.FieldId) && r.UseSelectorDirective))
                    relationship = CurrentEntity.GetParentSearchRelationship(field);

                if (field.FieldType == FieldType.Enum || relationship != null || field.FieldType == FieldType.Bit)
                {
                    if (field.FieldType == FieldType.Enum)
                    {
                        appSelectFilters += $"                    <div class=\"col-sm-6 col-md-6 col-lg-4\" *ngIf=\"!{field.Name.ToCamelCase()}\">" + Environment.NewLine;
                        appSelectFilters += $"                        <select id=\"{field.Name.ToCamelCase()}\" name=\"{field.Name.ToCamelCase()}\" [(ngModel)]=\"searchOptions.{field.Name.ToCamelCase()}\" #{field.Name.ToCamelCase()}=\"ngModel\" class=\"form-select\">" + Environment.NewLine;
                        appSelectFilters += $"                            <option *ngFor=\"let {field.Lookup.Name.ToCamelCase()} of {field.Lookup.PluralName.ToCamelCase()}\" [ngValue]=\"{field.Lookup.Name.ToCamelCase()}.value\">{{{{ {field.Lookup.Name.ToCamelCase()}.label }}}}</option>" + Environment.NewLine;
                        appSelectFilters += $"                        </select>" + Environment.NewLine;
                        appSelectFilters += $"                    </div>" + Environment.NewLine;
                    }
                    else if (field.FieldType == FieldType.Bit)
                    {
                        appSelectFilters += $"                    <div class=\"col-sm-6 col-md-4 col-lg-4 col-xl-3\">" + Environment.NewLine;
                        appSelectFilters += $"                        <select id=\"{field.Name.ToCamelCase()}\" name=\"{field.Name.ToCamelCase()}\" [(ngModel)]=\"searchOptions.{field.Name.ToCamelCase()}\" #{field.Name.ToCamelCase()}=\"ngModel\" class=\"form-select\">" + Environment.NewLine;
                        appSelectFilters += $"                            <option [ngValue]=\"undefined\">{field.Label}: Any</option>" + Environment.NewLine;
                        appSelectFilters += $"                            <option [ngValue]=\"true\">{field.Label}: Yes</option>" + Environment.NewLine;
                        appSelectFilters += $"                            <option [ngValue]=\"false\">{field.Label}: No</option>" + Environment.NewLine;
                        appSelectFilters += $"                        </select>" + Environment.NewLine;
                        appSelectFilters += $"                    </div>" + Environment.NewLine;
                    }
                    else
                    {
                        appSelectFilters += $"                    <div class=\"col-sm-6 col-md-6 col-lg-4\" *ngIf=\"!{relationship.ParentName.ToCamelCase()}\">" + Environment.NewLine;
                        appSelectFilters += $"                        <div class=\"form-group\">" + Environment.NewLine;
                        appSelectFilters += $"                            {relationship.AppSelector}" + Environment.NewLine;
                        appSelectFilters += $"                        </div>" + Environment.NewLine;
                        appSelectFilters += $"                    </div>" + Environment.NewLine;
                    }
                    appSelectFilters += Environment.NewLine;

                    if (filterAlerts == string.Empty) filterAlerts = Environment.NewLine;

                    if (relationship != null)
                        filterAlerts += $"                <div class=\"alert alert-info alert-dismissible\" *ngIf=\"showFilters && {relationship.ParentName.ToCamelCase()}!=undefined\">Filtered by {field.Label.ToLower()}: {{{{{relationship.ParentName.ToCamelCase()}.{relationship.ParentField.Name.ToCamelCase()}}}}}<button type=\"button\" class=\"btn-close\" data-bs-dismiss=\"alert\" (click)=\"searchOptions.{field.Name.ToCamelCase()}=undefined;{relationship.ParentName.ToCamelCase()}=undefined;runSearch();\" *ngIf=\"canRemoveFilters\"></button></div>" + Environment.NewLine;
                    else if (field.FieldType != FieldType.Bit)
                        filterAlerts += $"                <div class=\"alert alert-info alert-dismissible\" *ngIf=\"showFilters && {field.Name.ToCamelCase()}!=undefined\">Filtered by {field.Label.ToLower()}: {{{{{field.Name.ToCamelCase()}.label}}}}<button type=\"button\" class=\"btn-close\" data-bs-dismiss=\"alert\" (click)=\"searchOptions.{field.Name.ToCamelCase()}=undefined;{field.Name.ToCamelCase()}=undefined;runSearch();\" *ngIf=\"canRemoveFilters\"></button></div>" + Environment.NewLine;
                }
            }

            file = RunTemplateReplacements(file.Replace("APP_TEXT_FILTER", appTextFilter))
                .Replace("FIELD_HEADERS", fieldHeaders)
                .Replace("FIELD_LIST", fieldList)
                .Replace("APP_SELECT_FILTERS", appSelectFilters)
                .Replace("FILTER_ALERTS", filterAlerts);

            s.Add(file);

            return RunCodeReplacements(s.ToString(), CodeType.SelectModalHtml);
        }
    }
}