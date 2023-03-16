using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace WEB.Models
{
    public partial class Code
    {
        public string GenerateEditHtml()
        {
            var relationshipsAsParent = CurrentEntity.RelationshipsAsParent.Where(r => !r.ChildEntity.Exclude && r.DisplayListOnParent && r.Hierarchy).OrderBy(r => r.SortOrder);
            var hasChildRoutes = relationshipsAsParent.Any() || CurrentEntity.UseChildRoutes;

            var s = new StringBuilder();

            var t = string.Empty;
            if (hasChildRoutes)
            {
                s.Add($"<ng-container *ngIf=\"route.children.length === 0\">");
                s.Add($"");
                t = "    ";
            }
            s.Add(t + $"<app-page-title title=\"{CurrentEntity.FriendlyName}\"></app-page-title>");
            s.Add($"");
            s.Add(t + $"<form id=\"form\" name=\"form\" (submit)=\"save(form)\" novalidate #form=\"ngForm\" [ngClass]=\"{{ 'was-validated': form.submitted }}\">");
            s.Add($"");

            s.Add(t + $"    <div class=\"card border-0\">");
            s.Add($"");
            s.Add(t + $"        <div class=\"card-body\">");
            s.Add($"");
            s.Add(t + $"            <fieldset class=\"group\">");
            s.Add($"");
            s.Add(t + $"                <div class=\"row g-3\">");
            s.Add($"");

            #region form fields
            foreach (var field in CurrentEntity.Fields.OrderBy(o => o.FieldOrder))
            {
                if (field.KeyField && field.CustomType != CustomType.String && !CurrentEntity.HasCompositePrimaryKey) continue;
                if (field.EditPageType == EditPageType.Exclude) continue;
                if (field.EditPageType == EditPageType.SortField) continue;
                if (field.EditPageType == EditPageType.CalculatedField) continue;
                if (field.EditPageType == EditPageType.FileContents) continue;

                var isAppSelect = CurrentEntity.RelationshipsAsChild.Any(r => r.RelationshipFields.Any(f => f.ChildFieldId == field.FieldId)) && CurrentEntity.GetParentSearchRelationship(field).UseSelectorDirective;

                if (CurrentEntity.RelationshipsAsChild.Any(r => r.RelationshipFields.Any(f => f.ChildFieldId == field.FieldId)))
                {
                    var relationship = CurrentEntity.GetParentSearchRelationship(field);
                    //var relationshipField = relationship.RelationshipFields.Single(f => f.ChildFieldId == field.FieldId);
                    if (relationship.Hierarchy) continue;
                }

                var fieldName = field.Name.ToCamelCase();

                // todo: allow an override in the user fields?
                var controlSize = field.EditPageClasses == null ? "col-sm-6 col-md-4" : field.EditPageClasses; 
                var tagType = "input";
                var attributes = new Dictionary<string, string>();
                var ngIf = string.Empty;

                attributes.Add("id", fieldName);
                attributes.Add("name", fieldName);
                attributes.Add("class", "form-control");

                // default = text
                attributes.Add("type", "text");

                var readOnly = field.EditPageType == EditPageType.ReadOnly;

                if (!readOnly || isAppSelect)
                    attributes.Add("[(ngModel)]", CurrentEntity.Name.ToCamelCase() + "." + fieldName);

                if (!readOnly)
                {
                    attributes.Add("#" + fieldName, "ngModel");
                    if (!field.IsNullable)
                        attributes.Add("required", null);
                    if (field.FieldId == CurrentEntity.PrimaryFieldId)
                        attributes.Add("(ngModelChange)", $"changeBreadcrumb()");
                    if (field.CustomType == CustomType.Number && field.Scale > 0)
                        attributes.Add("step", "any");

                    if (field.EditPageType == EditPageType.EditWhenNew) attributes.Add("[disabled]", "!isNew");

                    if (field.CustomType == CustomType.Number)
                    {
                        attributes["type"] = "number";
                    }
                    else if (field.CustomType == CustomType.Enum)
                    {
                        tagType = "select";
                        attributes.Remove("class");
                        attributes.Remove("type");
                        attributes.Add("class", "form-select");
                    }
                    else if (field.CustomType == CustomType.Colour)
                    {
                        tagType = "app-color";
                        attributes.Remove("type");
                        attributes.Remove("class");
                    }
                    else if (field.FieldType == FieldType.Date || field.FieldType == FieldType.SmallDateTime || field.FieldType == FieldType.DateTime)
                    {
                        attributes.Add("readonly", null);
                        //if (field.EditPageType == EditPageType.ReadOnly) attributes.Add("disabled", null);
                        attributes.Add("placeholder", "yyyy-mm-dd");
                        attributes.Add("ngbDatepicker", null);
                        attributes.Add("#dp" + field.Name, "ngbDatepicker");
                        attributes.Add("tabindex", "-1");
                        attributes.Add("(click)", "dp" + field.Name + ".toggle()");
                        attributes.Add("container", "body");
                    }
                    else
                    {
                        if (!CurrentEntity.RelationshipsAsChild.Any(r => r.RelationshipFields.Any(f => f.ChildFieldId == field.FieldId) && r.UseSelectorDirective))
                        {
                            if (field.Length > 0 && field.EditPageType != EditPageType.FileName) attributes.Add("maxlength", field.Length.ToString());
                            if (field.MinLength > 0) attributes.Add("minlength", field.MinLength.ToString());
                        }
                        if (field.RegexValidation != null)
                            attributes.Add("pattern", field.RegexValidation);
                    }
                }
                else
                {
                    // read only field properties:
                    attributes.Add("readonly", null);
                    if (!isAppSelect)
                    {
                        if (CurrentEntity.RelationshipsAsChild.Any(r => r.RelationshipFields.Any(f => f.ChildFieldId == field.FieldId)))
                        {
                            var relationship = CurrentEntity.GetParentSearchRelationship(field);
                            attributes.Add("value", "{{" + CurrentEntity.Name.ToCamelCase() + "." + relationship.ParentName.ToCamelCase() + "?." + relationship.ParentEntity.PrimaryField.Name.ToCamelCase() + "}}");
                        }
                        else if (field.FieldType == FieldType.Enum)
                            attributes.Add("value", $"{{{{{field.Lookup.PluralName.ToCamelCase()}[{CurrentEntity.Name.ToCamelCase()}.{fieldName.ToCamelCase()}]?.label}}}}");
                        else if (field.CustomType == CustomType.Date)
                            attributes.Add("value", $"{{{{{CurrentEntity.Name.ToCamelCase()}.{fieldName.ToCamelCase()} | momentPipe: '{field.DateFormatString}'}}}}");
                        else if (field.FieldType == FieldType.Money)
                            attributes.Add("value", $"{{{{{CurrentEntity.Name.ToCamelCase()}.{fieldName.ToCamelCase()} | currency }}}}");
                        else
                            attributes.Add("value", $"{{{{{CurrentEntity.Name.ToCamelCase()}.{fieldName.ToCamelCase()}}}}}");
                    }
                    else
                    {
                        attributes.Add("disabled", null);
                    }
                }

                if (field.CustomType == CustomType.Boolean)
                {
                    attributes["type"] = "checkbox";
                    attributes.Remove("required");
                    attributes["class"] = "form-check-input";
                }
                else if (field.CustomType == CustomType.String && (field.FieldType == FieldType.Text || field.FieldType == FieldType.nText || field.Length == 0))
                {
                    tagType = "textarea";
                    attributes.Remove("type");
                    attributes.Add("rows", "5");
                }
                else if (field.EditPageType == EditPageType.FileName)
                {
                    tagType = "app-file";

                    //ngIf = " *ngIf=\"isNew\"";
                    field.Label = field.Label;
                    attributes.Remove("type");
                    attributes.Remove("class");

                    var fileContentsField = CurrentEntity.Fields.SingleOrDefault(o => o.EditPageType == EditPageType.FileContents);
                    attributes.Add("[(fileContents)]", $"{CurrentEntity.Name.ToCamelCase()}.{fileContentsField.Name.ToCamelCase()}");
                    attributes.Add("[enableDownload]", $"!isNew && !!{CurrentEntity.Name.ToCamelCase()}.{field.Name.ToCamelCase()}");
                    attributes.Add("[fileId]", $"{CurrentEntity.KeyFields.Select(o => $"{CurrentEntity.Name.ToCamelCase()}.{o.Name.ToCamelCase()}").Aggregate((current, next) => { return current + "/" + next; })}");
                    attributes.Add("(onDownload)", $"download($event)");

                }




                if (CurrentEntity.RelationshipsAsChild.Any(r => r.RelationshipFields.Any(f => f.ChildFieldId == field.FieldId)))
                {
                    var relationship = CurrentEntity.GetParentSearchRelationship(field);
                    var relationshipField = relationship.RelationshipFields.Single(f => f.ChildFieldId == field.FieldId);
                    if (!relationship.UseSelectorDirective)
                    {
                        // this was only for Read only's - needs to go above
                        //if (field.EditPageType == EditPageType.ReadOnly)
                        //{
                        //    attributes.Remove("[(ngModel)]");
                        //    attributes.Add("value", "{{" + CurrentEntity.Name.ToCamelCase() + "." + relationship.ParentName.ToCamelCase() + "?." + relationship.ParentEntity.PrimaryField.Name.ToCamelCase() + "}}");
                        //}
                    }
                    else if (!relationship.Hierarchy)
                    {
                        tagType = relationship.ParentEntity.Name.Hyphenated() + "-select";
                        if (attributes.ContainsKey("type")) attributes.Remove("type");
                        if (attributes.ContainsKey("class")) attributes.Remove("class");
                        attributes.Add($"[({relationship.ParentEntity.Name.ToCamelCase()})]", $"{relationship.ChildEntity.Name.ToCamelCase()}.{relationship.ParentName.ToCamelCase()}");
                    }
                }

                s.Add(t + $"                    <div class=\"{controlSize}\"{ngIf}>");
                s.Add(t + $"                        <div class=\"form-group\"{(readOnly ? "" : $" [ngClass]=\"{{ 'is-invalid': {fieldName}.invalid }}\"")}>");
                s.Add($"");
                s.Add(t + $"                            <label for=\"{fieldName.ToCamelCase()}\">");
                s.Add(t + $"                                {field.Label}:");
                s.Add(t + $"                            </label>");
                s.Add($"");

                var controlHtml = $"<{tagType}";
                foreach (var attribute in attributes)
                {
                    controlHtml += " " + attribute.Key;
                    if (attribute.Value != null) controlHtml += $"=\"{attribute.Value}\"";
                }
                if (tagType == "input")
                    controlHtml += " />";
                else if (tagType == "select")
                {
                    controlHtml += $">" + Environment.NewLine;
                    controlHtml += t + $"                                <option *ngFor=\"let {field.Lookup.Name.ToCamelCase()} of {field.Lookup.PluralName.ToCamelCase()}\" [ngValue]=\"{field.Lookup.Name.ToCamelCase()}.value\">{{{{ {field.Lookup.Name.ToCamelCase()}.label }}}}</option>" + Environment.NewLine;
                    controlHtml += t + $"                            </{tagType}>";
                }
                //";
                else
                    controlHtml += $"></{tagType}>";

                if (attributes.ContainsKey("type") && attributes["type"] == "checkbox")
                {
                    s.Add(t + $"                            <div class=\"form-check\">");
                    s.Add(t + $"                                {controlHtml}");
                    s.Add(t + $"                                <label class=\"form-check-label\" for=\"{field.Name.ToCamelCase()}\">");
                    s.Add(t + $"                                    {field.Label}");
                    s.Add(t + $"                                </label>");
                    s.Add(t + $"                            </div>");
                }
                else if (field.CustomType == CustomType.Date && !readOnly)
                {
                    s.Add(t + $"                            <div class=\"input-group\">");
                    s.Add(t + $"                                {controlHtml}");
                    s.Add(t + $"                                <button class=\"btn btn-secondary calendar\" (click)=\"dp{field.Name}.toggle()\" type=\"button\"><i class=\"fas fa-fw fa-calendar-alt\"></i></button>");
                    s.Add(t + $"                            </div>");
                }
                else if (field.FieldType == FieldType.VarBinary && field.EditPageType == EditPageType.FileContents)
                {
                    var fileNameField = CurrentEntity.Fields.FirstOrDefault(o => o.EditPageType == EditPageType.FileName);
                    if (fileNameField == null) throw new Exception(CurrentEntity.Name + ": FileContents field doesn't have a matching FileName field");

                    s.Add(t + $"                            <div class=\"input-group\">");
                    s.Add(t + $"                                <div class=\"input-group-prepend\" *ngIf=\"!isNew\">");
                    s.Add(t + $"                                    <button type=\"button\" class=\"btn btn-outline-primary\" (click)=\"download()\"><i class=\"fa fa-fw fa-cloud-download-alt\"></i></button>");
                    s.Add(t + $"                                </div>");
                    s.Add(t + $"                                <div class=\"custom-file\">");
                    s.Add(t + $"                                    {controlHtml}");
                    s.Add(t + $"                                    <label class=\"custom-file-label\" for=\"{field.Name.ToCamelCase()}\">{{{{{CurrentEntity.Name.ToCamelCase()}.{fileNameField.Name.ToCamelCase()} || \"Choose file\"}}}}</label>");
                    s.Add(t + $"                                </div>");
                    s.Add(t + $"                            </div>");
                }
                else
                    s.Add(t + $"                            {controlHtml}");


                s.Add($"");

                if (!readOnly)
                {
                    var validationErrors = new Dictionary<string, string>();
                    if (!field.IsNullable && field.CustomType != CustomType.Boolean && field.EditPageType != EditPageType.ReadOnly)
                    {
                        if (field.EditPageType == EditPageType.FileName) validationErrors.Add("required", $"A file is required");
                        else validationErrors.Add("required", $"{field.Label} is required");
                    }
                    if (!CurrentEntity.RelationshipsAsChild.Any(r => r.RelationshipFields.Any(f => f.ChildFieldId == field.FieldId) && r.UseSelectorDirective))
                    {
                        if (field.MinLength > 0) validationErrors.Add("minlength", $"{field.Label} must be at least {field.MinLength} characters long");
                        if (field.Length > 0 && field.EditPageType != EditPageType.FileName) validationErrors.Add("maxlength", $"{field.Label} must be at most {field.Length} characters long");
                    }
                    if (field.RegexValidation != null) validationErrors.Add("pattern", $"{field.Label} does not match the specified pattern");

                    foreach (var validationError in validationErrors)
                    {
                        s.Add(t + $"                            <div *ngIf=\"{fieldName}.errors?.{validationError.Key}\" class=\"invalid-feedback\">");
                        s.Add(t + $"                                {validationError.Value}");
                        s.Add(t + $"                            </div>");
                        s.Add($"");
                    }
                }

                s.Add(t + $"                        </div>");
                s.Add(t + $"                    </div>");
                s.Add($"");

            }
            #endregion

            if (CurrentEntity.EntityType == EntityType.User)
            {
                s.Add(t + $"                    <div class=\"col-sm-6 col-md-4\">");
                s.Add(t + $"                        <div class=\"form-group\">");
                s.Add($"");
                s.Add(t + $"                            <label>");
                s.Add(t + $"                                Roles:");
                s.Add(t + $"                            </label>");
                s.Add($"");
                s.Add(t + $"                            <select id=\"roles\" name=\"roles\" [multiple]=\"true\" class=\"form-control\" [(ngModel)]=\"user.roles\">");
                s.Add(t + $"                                <option *ngFor=\"let role of roles\" [ngValue]=\"role.name\">{{{{role.label}}}}</option>");
                s.Add(t + $"                            </select>");
                s.Add($"");
                s.Add(t + $"                        </div>");
                s.Add(t + $"                    </div>");
                s.Add($"");
            }

            s.Add(t + $"                </div>");
            s.Add($"");
            s.Add(t + $"            </fieldset>");
            s.Add($"");

            s.Add(t + $"            <fieldset class=\"my-3\">");
            s.Add(t + $"                <button type=\"submit\" class=\"btn btn-outline-success me-2 mb-1\">Save<i class=\"fas fa-check ms-2\"></i></button>");
            s.Add(t + $"                <button type=\"button\" *ngIf=\"!isNew\" class=\"btn btn-outline-danger me-2 mb-1\" (click)=\"delete()\">Delete<i class=\"fas fa-times ms-2\"></i></button>");
            s.Add(t + $"            </fieldset>");
            s.Add($"");

            s.Add(t + $"        </div>");
            s.Add($"");
            s.Add(t + $"    </div>");
            s.Add($"");

            s.Add(t + $"</form>");
            s.Add($"");

            #region child lists
            if (CurrentEntity.RelationshipsAsParent.Any(r => !r.ChildEntity.Exclude && r.DisplayListOnParent))
            {
                var relationships = CurrentEntity.RelationshipsAsParent.Where(r => !r.ChildEntity.Exclude && r.DisplayListOnParent).OrderBy(r => r.SortOrder);
                var counter = 0;

                s.Add(t + $"<ng-container *ngIf=\"!isNew\">");
                s.Add($"");
                s.Add(t + $"    <hr />");
                s.Add($"");
                s.Add(t + $"    <nav ngbNav #nav=\"ngbNav\" class=\"nav-tabs\">");
                s.Add($"");
                foreach (var relationship in relationships)
                {
                    counter++;
                    var entity = relationship.ChildEntity;

                    s.Add(t + $"        <ng-container ngbNavItem class=\"pt-6\">");
                    s.Add($"");
                    s.Add(t + $"            <a ngbNavLink>{relationship.CollectionFriendlyName}</a>");
                    s.Add($"");
                    s.Add(t + $"            <ng-template ngbNavContent>");
                    s.Add($"");
                    s.Add(t + $"                <div class=\"card border-0 search-results\">");
                    s.Add($"");
                    s.Add(t + $"                    <div class=\"card-header border-0\">");
                    s.Add($"");
                    s.Add(t + $"                        <h4 class=\"card-header-title text-uppercase\">");
                    s.Add(t + $"                            {relationship.CollectionFriendlyName}");
                    s.Add(t + $"                        </h4>");
                    s.Add($"");
                    s.Add(t + $"                    </div>");
                    s.Add($"");

                    s.Add(t + $"                    <div class=\"card-body\">");
                    s.Add($"");

                    // todo: non-text filter fields
                    if (entity.Fields.Any(f => f.SearchType == SearchType.Text))
                    {
                        s.Add(t + $"                        <form id=\"formSearch{relationship.CollectionName}\" (submit)=\"search{relationship.CollectionName}(0)\" novalidate *ngIf=\"show{relationship.CollectionName}Search\" class=\"mb-5\">");
                        s.Add($"");
                        s.Add(t + $"                            <div class=\"row g-3\">");
                        s.Add($"");

                        if (entity.Fields.Any(f => f.SearchType == SearchType.Text))
                        {
                            s.Add(t + $"                                <div class=\"col-sm-6 col-md-5 col-lg-4 col-xl-3\">");
                            s.Add(t + $"                                    <div class=\"form-group\">");
                            s.Add(t + $"                                        <input type=\"search\" name=\"q\" id=\"q\" [(ngModel)]=\"{relationship.CollectionName.ToCamelCase()}SearchOptions.q\" max=\"100\" class=\"form-control\" placeholder=\"Search {relationship.CollectionFriendlyName.ToLower()}\" />");
                            s.Add(t + $"                                    </div>");
                            s.Add(t + $"                                </div>");
                            s.Add($"");
                        }

                        s.Add(t + $"                            </div>");
                        s.Add($"");
                        s.Add(t + $"                        </form>");
                        s.Add($"");
                    }

                    var childEntity = relationship.ChildEntity;

                    if (relationship.UseMultiSelect)
                    {
                        s.Add(t + $"                    <div class=\"mb-3\">");
                        s.Add(t + $"                        <button class=\"btn btn-outline-primary me-2 mb-1\" (click)=\"add{relationship.CollectionName}()\">Add {relationship.CollectionFriendlyName}<i class=\"fas fa-plus ms-2\"></i></button>");
                        s.Add(t + $"                    </div>");
                        s.Add($"");
                    }
                    else if (relationship.Hierarchy)
                    {
                        // trying to get this to work for instances like African POT Project->Team hierarchy, where I only want 1 add for the userId
                        s.Add(t + $"                        <div class=\"mb-3\">");
                        s.Add(t + $"                            <a [routerLink]=\"['./{childEntity.PluralName.ToLower()}', 'add']\" class=\"btn btn-outline-primary me-2 mb-1\">Add<i class=\"fas fa-plus ms-2\"></i></a>");
                        s.Add(t + $"                            <button *ngIf=\"!show{relationship.CollectionName}Search\" type=\"button\" class=\"btn btn-outline-secondary me-2 mb-1\" (click)=\"show{relationship.CollectionName}Search=true\">Filter<i class=\"fas fa-filter ms-2\"></i></button>");
                        s.Add(t + $"                            <button *ngIf=\"show{relationship.CollectionName}Search\" form=\"form{relationship.CollectionName}\" type=\"submit\" class=\"btn btn-outline-primary me-2 mb-1\">Search<i class=\"fas fa-search ms-2\"></i></button>");
                        if (childEntity.HasASortField)
                            s.Add(t + $"                            <button type=\"button\" class=\"btn btn-outline-secondary me-2 mb-1\" (click)=\"show{childEntity.Name}Sort()\" *ngIf=\"{relationship.CollectionName.ToCamelCase()}Headers.totalRecords > 1 && !show{relationship.CollectionName}Search\">Sort<i class=\"fas fa-sort ms-2\"></i></button>");
                        s.Add(t + $"                        </div>");
                        s.Add($"");
                    }
                    s.Add(t + $"                    </div>");
                    s.Add($"");

                    #region table
                    s.Add(t + $"                    <div class=\"table-responsive\">");
                    s.Add($"");
                    s.Add(t + $"                        <table class=\"table table-hover table-edge table-nowrap mb-0 align-middle\">");
                    s.Add(t + $"                            <thead class=\"thead-light\">");
                    s.Add(t + $"                                <tr>");
                    if (relationship.UseMultiSelect)
                    {
                        var reverseRel = relationship.ChildEntity.RelationshipsAsChild.Where(o => o.RelationshipId != relationship.RelationshipId).SingleOrDefault();

                        s.Add(t + $"                                    <th>{reverseRel.ParentFriendlyName}</th>");
                    }
                    else
                    {
                        foreach (var column in childEntity.GetSearchResultsFields(CurrentEntity))
                        {
                            s.Add(t + $"                                    <th>{column.Header}</th>");
                        }
                    }
                    s.Add(t + $"                                    <th class=\"w-20px text-center\"><i class=\"fas fa-times text-danger cursor-pointer\" (click)=\"delete{relationship.CollectionName}()\" ngbTooltip=\"Delete all {relationship.CollectionFriendlyName.ToLower()}\" container=\"body\" placement=\"left\"></i></th>");
                    s.Add(t + $"                                </tr>");
                    s.Add(t + $"                            </thead>");
                    s.Add(t + $"                            <tbody class=\"list cursor-pointer\">");
                    s.Add(t + $"                                <tr *ngFor=\"let {childEntity.Name.ToCamelCase()} of {relationship.CollectionName.ToCamelCase()}\" (click)=\"goTo{relationship.CollectionSingular}({childEntity.Name.ToCamelCase()})\">");
                    // this was added for TrainTrack entityLinks; not sure how it will affect other projects!
                    if (relationship.UseMultiSelect)
                    {
                        var reverseRel = relationship.ChildEntity.RelationshipsAsChild.Where(o => o.RelationshipId != relationship.RelationshipId).SingleOrDefault();

                        s.Add(t + $"                                    <td>{{{{ {relationship.ChildEntity.Name.ToCamelCase()}.{reverseRel.ParentName.ToCamelCase()}.{reverseRel.ParentEntity.PrimaryField.Name.ToCamelCase()} }}}}</td>");
                    }
                    else
                    {
                        foreach (var column in childEntity.GetSearchResultsFields(CurrentEntity))
                        {
                            s.Add(t + $"                                    <td>{column.Value}</td>");
                        }
                    }
                    s.Add(t + $"                                    <td class=\"text-center\"><i class=\"fas fa-times cursor-pointer p-1 text-danger\" (click)=\"delete{relationship.CollectionSingular}({relationship.ChildEntity.Name.ToCamelCase()}, $event)\"></i></td>");
                    s.Add(t + $"                                </tr>");
                    s.Add(t + $"                            </tbody>");
                    s.Add(t + $"                        </table>");
                    s.Add($"");

                    s.Add(t + $"                    </div>");
                    s.Add($"");

                    s.Add(t + $"                    <div class=\"card-footer\">");
                    s.Add(t + $"                        <pager [headers]=\"{relationship.CollectionName.ToCamelCase()}Headers\" (pageChanged)=\"search{relationship.CollectionName}($event)\"></pager>");
                    s.Add(t + $"                    </div>");
                    s.Add($"");

                    s.Add(t + $"                </div>");
                    s.Add($"");
                    #endregion

                    s.Add(t + $"            </ng-template>");
                    s.Add($"");
                    s.Add(t + $"        </ng-container>");
                    s.Add($"");
                }
                s.Add(t + $"    </nav>");
                s.Add($"");
                s.Add(t + $"    <div [ngbNavOutlet]=\"nav\" class=\"mt-1\"></div>");
                s.Add($"");
                s.Add(t + $"</ng-container>");
                s.Add($"");
            }
            #endregion

            if (hasChildRoutes)
            {
                s.Add($"</ng-container>");
                s.Add($"");
            }

            foreach (var rel in CurrentEntity.RelationshipsAsParent.Where(o => o.UseMultiSelect && !o.ChildEntity.Exclude))
            {
                var reverseRel = rel.ChildEntity.RelationshipsAsChild.Where(o => o.RelationshipId != rel.RelationshipId).SingleOrDefault();

                //[organisation]=\"user.organisation\"   [canRemoveFilters]=\"false\"
                s.Add($"<{reverseRel.ParentEntity.Name.Hyphenated()}-modal #{reverseRel.ParentEntity.Name.ToCamelCase()}Modal (changes)=\"change{reverseRel.ParentEntity.Name}($event)\" [multiple]=\"true\"></{reverseRel.ParentEntity.Name.Hyphenated()}-modal>");
                s.Add($"");
            }

            if (hasChildRoutes)
            {
                s.Add($"<router-outlet></router-outlet>");
                s.Add($"");
            }

            return RunCodeReplacements(s.ToString(), CodeType.EditHtml);
        }
    }
}