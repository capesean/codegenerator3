using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace WEB.Models
{
    public partial class Code
    {
        public string GenerateEditTypeScript()
        {
            var folders = string.Join("", Enumerable.Repeat("../", CurrentEntity.Project.GeneratedPath.Count(o => o == '/')));

            var multiSelectRelationships = CurrentEntity.RelationshipsAsParent.Where(r => r.UseMultiSelect && !r.ChildEntity.Exclude).OrderBy(o => o.SortOrder);
            var relationshipsAsParent = CurrentEntity.RelationshipsAsParent.Where(r => !r.ChildEntity.Exclude && r.DisplayListOnParent).OrderBy(r => r.SortOrder);
            var relationshipsAsChildHierarchy = CurrentEntity.RelationshipsAsChild.FirstOrDefault(r => r.Hierarchy);
            var enumLookups = CurrentEntity.Fields.Where(o => o.FieldType == FieldType.Enum).OrderBy(o => o.FieldOrder).Select(o => o.Lookup).Distinct().ToList();
            var nonHKeyFields = CurrentEntity.GetNonHierarchicalKeyFields();
            // add lookups for table/search fields on children entities
            foreach (var rel in CurrentEntity.RelationshipsAsParent.Where(r => !r.ChildEntity.Exclude && r.DisplayListOnParent).OrderBy(r => r.SortOrder))
                foreach (var field in rel.ChildEntity.Fields.Where(o =>
                    o.FieldType == FieldType.Enum           // field is an enum
                    && !enumLookups.Contains(o.Lookup)      // enum hasn't already been added
                    && (o.ShowInSearchResults || o.SearchType == SearchType.Exact)
                    ))
                        enumLookups.Add(field.Lookup);
            // this is for the breadcrumb being on the parent entity which has a primary field of type enum
            var addEnum = CurrentEntity.RelationshipsAsChild.SingleOrDefault(r => r.RelationshipFields.Count == 1 && r.RelationshipFields.First().ChildFieldId == CurrentEntity.PrimaryField.FieldId)?.ParentEntity?.PrimaryField?.FieldType == FieldType.Enum;
            if (!addEnum)
            {
                // can't get this to work: 
                // intention: on CurrentEntity, a child entity list has a field that is a relationship-link to another 
                if (CurrentEntity.RelationshipsAsParent.Any(o => (o.DisplayListOnParent || o.Hierarchy) && o.ChildEntity.Fields.Any(f => f.ShowInSearchResults && f.RelationshipFieldsAsChild.Any(rf => rf.ParentField.Entity.PrimaryField.FieldType == FieldType.Enum))))
                    addEnum = true;
            }

            var hasChildRoutes = relationshipsAsParent.Any(o => o.Hierarchy) || CurrentEntity.UseChildRoutes;
            var hasFileContents = CurrentEntity.Fields.Any(o => o.EditPageType == EditPageType.FileContents && o.CustomType == CustomType.Binary);

            var s = new StringBuilder();

            s.Add($"import {{ Component, OnInit{(hasChildRoutes ? ", OnDestroy" : "") + (multiSelectRelationships.Any() ? ", ViewChild" : "")} }} from '@angular/core';");
            s.Add($"import {{ Router, ActivatedRoute{(hasChildRoutes ? ", NavigationEnd" : "")} }} from '@angular/router';");
            s.Add($"import {{ ToastrService }} from 'ngx-toastr';");
            s.Add($"import {{ NgForm }} from '@angular/forms';");
            if (relationshipsAsParent.Any())
                s.Add($"import {{ Subject{(hasChildRoutes || CurrentEntity.EntityType == EntityType.User ? ", Subscription" : "")} }} from 'rxjs';");
            s.Add($"import {{ HttpErrorResponse }} from '@angular/common/http';");
            if (CurrentEntity.EntityType != EntityType.Settings)
                s.Add($"import {{ NgbModal }} from '@ng-bootstrap/ng-bootstrap';");

            if (CurrentEntity.EntityType != EntityType.Settings)
                s.Add($"import {{ ConfirmModalComponent, ModalOptions }} from '{folders}../common/components/confirm.component';");

            if (relationshipsAsParent.Any())
                s.Add($"import {{ PagingHeaders }} from '{folders}../common/models/http.model';");
            s.Add($"import {{ {CurrentEntity.Name} }} from '{folders}../common/models/{CurrentEntity.Name.ToLower()}.model';");
            if (CurrentEntity.EntityType != EntityType.User && (enumLookups.Count > 0 || addEnum))
                s.Add($"import {{ Enum, Enums{(CurrentEntity.PrimaryField.FieldType == FieldType.Enum ? ", " + CurrentEntity.PrimaryField.Lookup.PluralName : "")} }} from '{folders}../common/models/enums.model';");

            if (CurrentEntity.EntityType == EntityType.User)
            {
                s.Add($"import {{ Enum, Enums, Roles }} from '{folders}../common/models/enums.model';");
                s.Add($"import {{ ProfileModel }} from '{folders}../common/models/profile.models';");
                s.Add($"import {{ AuthService }} from '{folders}../common/services/auth.service';");
            }

            if (CurrentEntity.EntityType != EntityType.Settings)
                s.Add($"import {{ BreadcrumbService }} from '{folders}../common/services/breadcrumb.service';");
            else
                s.Add($"import {{ AppService }} from '{folders}../common/services/app.service';");
            s.Add($"import {{ ErrorService }} from '{folders}../common/services/error.service';");
            s.Add($"import {{ {CurrentEntity.Name}Service }} from '{folders}../common/services/{CurrentEntity.Name.ToLower()}.service';");

            foreach (var relChildEntity in relationshipsAsParent.Select(o => o.ChildEntity).Distinct().OrderBy(o => o.Name))
            {
                s.Add($"import {{ {(relChildEntity.EntityId == CurrentEntity.EntityId ? "" : relChildEntity.Name + ", ")}{relChildEntity.Name}SearchOptions, {relChildEntity.Name}SearchResponse }} from '{folders}../common/models/{relChildEntity.Name.ToLower()}.model';");
                if (relChildEntity.EntityId != CurrentEntity.EntityId)
                    s.Add($"import {{ {relChildEntity.Name}Service }} from '{folders}../common/services/{relChildEntity.Name.ToLower()}.service';");
            }
            var processedEntityIds = new List<Guid>();
            foreach (var rel in multiSelectRelationships)
            {
                if (processedEntityIds.Contains(rel.ChildEntityId)) continue;
                processedEntityIds.Add(rel.ChildEntityId);

                var reverseRel = rel.ChildEntity.RelationshipsAsChild.Where(o => o.RelationshipId != rel.RelationshipId).SingleOrDefault();

                s.Add($"import {{ {reverseRel.ParentEntity.Name}ModalComponent }} from '../{reverseRel.ParentEntity.PluralName.ToLower()}/{reverseRel.ParentEntity.Name.ToLower()}.modal.component';");
                if (reverseRel.ParentEntity != CurrentEntity) s.Add($"import {{ {reverseRel.ParentEntity.Name} }} from '{folders}../common/models/{reverseRel.ParentEntity.Name.ToLower()}.model';");
            }
            if (relationshipsAsParent.Any(o => o.Hierarchy && o.ChildEntity.HasASortField))
            {
                foreach (var entity in relationshipsAsParent.Where(o => o.Hierarchy && o.ChildEntity.HasASortField).Select(o => o.ChildEntity).Distinct())
                    s.Add($"import {{ {entity.Name}SortComponent }} from '../{entity.PluralName.ToLower()}/{entity.Name.ToLower()}.sort.component';");
            }
            if (CurrentEntity.PrimaryField.CustomType == CustomType.Date)
                s.Add($"import * as moment from 'moment';");
            if (hasFileContents)
                s.Add($"import {{ DownloadService }} from '{folders}../common/services/download.service';");

            s.Add($"");

            s.Add($"@Component({{");
            s.Add($"    selector: '{CurrentEntity.Name.ToLower()}-edit',");
            s.Add($"    templateUrl: './{CurrentEntity.Name.ToLower()}.edit.component.html'");
            s.Add($"}})");

            s.Add($"export class {CurrentEntity.Name}EditComponent implements OnInit{(hasChildRoutes ? ", OnDestroy" : "")} {{");
            s.Add($"");
            s.Add($"    public {CurrentEntity.Name.ToCamelCase()}: {CurrentEntity.Name} = new {CurrentEntity.Name}();");
            if (CurrentEntity.EntityType != EntityType.Settings)
                s.Add($"    public isNew = true;");
            if (hasChildRoutes)
                s.Add($"    private routerSubscription: Subscription;");
            foreach (var enumLookup in enumLookups)
                s.Add($"    public {enumLookup.PluralName.ToCamelCase()}: Enum[] = Enums.{enumLookup.PluralName};");
            if (CurrentEntity.EntityType == EntityType.User)
            {
                s.Add($"    public roles: Enum[] = Enums.Roles;");
                s.Add($"    private profile: ProfileModel;");
            }
            //foreach(var x in CurrentEntity.RelationshipsAsParent.Where(o => (o.DisplayListOnParent || o.Hierarchy) && o.ChildEntity.Fields.Any(f => f.ShowInSearchResults && f.RelationshipFieldsAsChild.Any(rf => rf.ParentField.Entity.PrimaryField.FieldType == FieldType.Enum)
            s.Add($"");
            foreach (var rel in relationshipsAsParent)
            {
                s.Add($"    public {rel.CollectionName.ToCamelCase()}SearchOptions = new {rel.ChildEntity.Name}SearchOptions();");
                s.Add($"    public {rel.CollectionName.ToCamelCase()}Headers = new PagingHeaders();");
                s.Add($"    public {rel.CollectionName.ToCamelCase()}: {rel.ChildEntity.Name}[] = [];");
                s.Add($"    public show{rel.CollectionName}Search = false;");
                s.Add($"");
            }
            processedEntityIds.Clear();
            foreach (var rel in multiSelectRelationships)
            {
                if (processedEntityIds.Contains(rel.ChildEntityId)) continue;
                processedEntityIds.Add(rel.ChildEntityId);

                var reverseRel = rel.ChildEntity.RelationshipsAsChild.Where(o => o.RelationshipId != rel.RelationshipId).SingleOrDefault();
                s.Add($"    @ViewChild('{reverseRel.ParentEntity.Name.ToCamelCase()}Modal') {reverseRel.ParentEntity.Name.ToCamelCase()}Modal: {reverseRel.ParentEntity.Name}ModalComponent;");
            }
            if (multiSelectRelationships.Any())
                s.Add($"");

            s.Add($"    constructor(");
            s.Add($"        private router: Router,");
            s.Add($"        {(hasChildRoutes ? "public" : "private")} route: ActivatedRoute,");
            s.Add($"        private toastr: ToastrService,");
            if (CurrentEntity.EntityType != EntityType.Settings)
            {
                s.Add($"        private breadcrumbService: BreadcrumbService,");
                s.Add($"        private modalService: NgbModal,");
            }
            else
            {
                s.Add($"        private appService: AppService,");
            }
            s.Add($"        private {CurrentEntity.Name.ToCamelCase()}Service: {CurrentEntity.Name}Service,");
            var relChildEntities = relationshipsAsParent.Where(o => o.ChildEntityId != CurrentEntity.EntityId).Select(o => o.ChildEntity).Distinct().OrderBy(o => o.Name);
            foreach (var relChildEntity in relChildEntities)
                s.Add($"        private {relChildEntity.Name.ToCamelCase()}Service: {relChildEntity.Name}Service,");
            if (CurrentEntity.EntityType == EntityType.User)
                s.Add($"        private authService: AuthService,");
            if (hasFileContents)
                s.Add($"        private downloadService: DownloadService,");

            s.Add($"        private errorService: ErrorService");
            s.Add($"    ) {{");
            s.Add($"    }}");
            s.Add($"");

            s.Add($"    ngOnInit(): void {{");
            s.Add($"");

            if (CurrentEntity.EntityType == EntityType.User)
            {
                s.Add($"        this.authService.getProfile().subscribe(profile => {{");
                s.Add($"            this.profile = profile;");
                s.Add($"        }});");
                s.Add($"");
            }

            if (CurrentEntity.EntityType == EntityType.Settings)
            {
                s.Add($"        this.loadSettings();");
                s.Add($"");
            }
            else
            {

                // use subscribe, so that a save changes url and reloads data
                s.Add($"        this.route.params.subscribe(params => {{");
                s.Add($"");
                //if (relationshipsAsChildHierarchy != null)
                //    foreach (var rfield in relationshipsAsChildHierarchy.RelationshipFields)
                //s.Add($"            const {rfield.ParentField.Name.ToCamelCase()} = this.route.snapshot.parent.params.{rfield.ParentField.Name.ToCamelCase()};");

                foreach (var keyField in nonHKeyFields)
                {
                    s.Add($"            const {keyField.Name.ToCamelCase()} = params[\"{keyField.Name.ToCamelCase()}\"];");
                }
                if (relationshipsAsChildHierarchy != null)
                {
                    foreach (var rfield in relationshipsAsChildHierarchy.RelationshipFields)
                        s.Add($"            this.{CurrentEntity.Name.ToCamelCase()}.{rfield.ChildField.Name.ToCamelCase()} = this.route.snapshot.parent.params.{rfield.ParentField.Name.ToCamelCase()};");
                }
                s.Add($"            this.isNew = {CurrentEntity.GetNonHierarchicalKeyFields().Select(o => o.Name.ToCamelCase() + " === \"add\"").Aggregate((current, next) => { return current + " && " + next; })};");

                s.Add($"");

                s.Add($"            if (!this.isNew) {{");
                s.Add($"");
                foreach (var keyField in nonHKeyFields)
                {
                    s.Add($"                this.{CurrentEntity.Name.ToCamelCase()}.{keyField.Name.ToCamelCase()} = {keyField.Name.ToCamelCase()};");
                }
                s.Add($"                this.load{CurrentEntity.Name}();");
                s.Add($"");
                foreach (var rel in relationshipsAsParent)
                {
                    foreach (var relField in rel.RelationshipFields)
                        s.Add($"                this.{rel.CollectionName.ToCamelCase()}SearchOptions.{relField.ChildField.Name.ToCamelCase()} = {relField.ParentField.Name.ToCamelCase()};");
                    s.Add($"                this.{rel.CollectionName.ToCamelCase()}SearchOptions.includeParents = true;");
                    s.Add($"                this.search{rel.CollectionName}();");
                    s.Add($"");
                }

                s.Add($"            }}");
                s.Add($"");
                if (hasChildRoutes)
                {
                    s.Add($"            this.routerSubscription = this.router.events.subscribe(event => {{");
                    s.Add($"                if (event instanceof NavigationEnd && !this.route.firstChild) {{");
                    // this was causing a 404 error after deleting
                    //s.Add($"                  this.load{CurrentEntity.Name}();");
                    // 
                    s.Add($"                    // this will double-load on new save, as params change (above) + nav ends");
                    foreach (var rel in relationshipsAsParent.Where(o => o.Hierarchy))
                    {
                        s.Add($"                    this.search{rel.CollectionName}();");
                    }
                    s.Add($"                }}");
                    s.Add($"            }});");
                    s.Add($"");
                }

                s.Add($"        }});");
                s.Add($"");
            }

            s.Add($"    }}");
            s.Add($"");

            if (hasChildRoutes)
            {
                s.Add($"    ngOnDestroy(): void {{");
                s.Add($"        this.routerSubscription.unsubscribe();");
                s.Add($"    }}");
                s.Add($"");
            }

            s.Add($"    private load{CurrentEntity.Name}(): void {{");
            s.Add($"");
            s.Add($"        this.{CurrentEntity.Name.ToCamelCase()}Service.get({(CurrentEntity.EntityType == EntityType.Settings ? string.Empty : CurrentEntity.KeyFields.Select(o => $"this.{CurrentEntity.Name.ToCamelCase()}.{o.Name.ToCamelCase()}").Aggregate((current, next) => { return current + ", " + next; }))})");
            s.Add($"            .subscribe({{");
            s.Add($"                next: {CurrentEntity.Name.ToCamelCase()} => {{");
            s.Add($"                    this.{CurrentEntity.Name.ToCamelCase()} = {CurrentEntity.Name.ToCamelCase()};");
            if (CurrentEntity.EntityType != EntityType.Settings)
                s.Add($"                    this.changeBreadcrumb();");
            s.Add($"                }},");
            s.Add($"                error: err => {{");
            s.Add($"                    this.errorService.handleError(err, \"{CurrentEntity.FriendlyName}\", \"Load\");");
            s.Add($"                    if (err instanceof HttpErrorResponse && err.status === 404)");
            s.Add($"                        {CurrentEntity.ReturnRoute}");
            s.Add($"                }}");
            s.Add($"            }});");
            s.Add($"");
            s.Add($"    }}");
            s.Add($"");

            s.Add($"    save(form: NgForm): void {{");
            s.Add($"");
            s.Add($"        if (form.invalid) {{");
            s.Add($"");
            s.Add($"            this.toastr.error(\"The form has not been completed correctly.\", \"Form Error\");");
            s.Add($"            return;");
            s.Add($"");
            s.Add($"        }}");
            s.Add($"");
            s.Add($"        this.{CurrentEntity.Name.ToCamelCase()}Service.save(this.{CurrentEntity.Name.ToCamelCase()})");
            s.Add($"            .subscribe({{");
            s.Add($"                next: {(CurrentEntity.ReturnOnSave ? "()" : CurrentEntity.Name.ToCamelCase())} => {{");
            s.Add($"                    this.toastr.success(\"The {CurrentEntity.FriendlyName.ToLower()} has been saved\", \"Save {CurrentEntity.FriendlyName}\");");
            if (CurrentEntity.EntityType != EntityType.Settings)
            {
                if (CurrentEntity.ReturnOnSave)
                    s.Add($"                    {CurrentEntity.ReturnRoute}");
                else
                {
                    if (hasChildRoutes)
                    {
                        s.Add($"                    if (this.isNew) {{");
                        s.Add($"                        this.ngOnDestroy();");
                        s.Add($"                        this.router.navigate([\"../\", {nonHKeyFields.Select(o => $"{CurrentEntity.Name.ToCamelCase()}.{o.Name.ToCamelCase()}").Aggregate((current, next) => { return current + ", " + next; })}], {{ relativeTo: this.route }});");
                        s.Add($"                    }}");
                    }
                    else
                    {
                        s.Add($"                    if (this.isNew) this.router.navigate([\"../\", {nonHKeyFields.Select(o => $"{CurrentEntity.Name.ToCamelCase()}.{o.Name.ToCamelCase()}").Aggregate((current, next) => { return current + ", " + next; })}], {{ relativeTo: this.route }});");
                    }
                }
                if (CurrentEntity.EntityType == EntityType.User)
                {
                    s.Add($"                    else {{");
                    s.Add($"                        // reload profile if editing self");
                    s.Add($"                        if (this.user.id === this.profile.userId)");
                    s.Add($"                            this.authService.getProfile(true).subscribe();");
                    s.Add($"                    }}");
                }
            }
            else
            {
                s.Add($"                    this.appService.getAppSettings(true).subscribe(); // refresh the appSettings");
            }
            //else if (!CurrentEntity.ReturnOnSave)
            //{
            //    s.Add($"                    }}");
            //}
            s.Add($"                }},");
            s.Add($"                error: err => {{");
            s.Add($"                    this.errorService.handleError(err, \"{CurrentEntity.FriendlyName}\", \"Save\");");
            s.Add($"                }}");
            s.Add($"            }});");
            s.Add($"");
            s.Add($"    }}");
            s.Add($"");

            if (CurrentEntity.EntityType != EntityType.Settings)
            {
                s.Add($"    delete(): void {{");
                s.Add($"");
                s.Add($"        let modalRef = this.modalService.open(ConfirmModalComponent, {{ centered: true }});");
                s.Add($"        (modalRef.componentInstance as ConfirmModalComponent).options = {{ title: \"Delete {CurrentEntity.FriendlyName}\", text: \"Are you sure you want to delete this {CurrentEntity.FriendlyName.ToLower()}?\", deleteStyle: true, ok: \"Delete\" }} as ModalOptions;");
                s.Add($"        modalRef.result.then(");
                s.Add($"            () => {{");
                s.Add($"");
                s.Add($"                this.{CurrentEntity.Name.ToCamelCase()}Service.delete({CurrentEntity.KeyFields.Select(o => $"this.{CurrentEntity.Name.ToCamelCase()}.{o.Name.ToCamelCase()}").Aggregate((current, next) => { return current + ", " + next; })})");
                s.Add($"                    .subscribe({{");
                s.Add($"                        next: () => {{");
                s.Add($"                            this.toastr.success(\"The {CurrentEntity.FriendlyName.ToLower()} has been deleted\", \"Delete {CurrentEntity.FriendlyName}\");");
                s.Add($"                            {CurrentEntity.ReturnRoute}");
                s.Add($"                        }},");
                s.Add($"                        error: err => {{");
                s.Add($"                            this.errorService.handleError(err, \"{CurrentEntity.FriendlyName}\", \"Delete\");");
                s.Add($"                        }}");
                s.Add($"                    }});");
                s.Add($"");
                s.Add($"            }}, () => {{ }});");
                s.Add($"    }}");
                s.Add($"");

                s.Add($"    changeBreadcrumb(): void {{");
                // if the 'primary field' is a foreign key to another entity
                //if()//CurrentEntity.PrimaryFieldId
                if (CurrentEntity.RelationshipsAsChild.Any(r => r.RelationshipFields.Count == 1 && r.RelationshipFields.First()?.ChildFieldId == CurrentEntity.PrimaryField.FieldId))
                {
                    var rel = CurrentEntity.RelationshipsAsChild.Single(r => r.RelationshipFields.Count == 1 && r.RelationshipFields.First().ChildFieldId == CurrentEntity.PrimaryField.FieldId);
                    var primaryField = rel.ParentEntity.PrimaryField;
                    if (primaryField.CustomType == CustomType.Enum)
                    {
                        s.Add($"        this.breadcrumbService.changeBreadcrumb(this.route.snapshot, this.{CurrentEntity.Name.ToCamelCase()}.{rel.ParentEntity.Name.ToCamelCase()}.{primaryField.Name.ToCamelCase()} !== undefined ? Enums.{primaryField.Lookup.PluralName}[this.{CurrentEntity.Name.ToCamelCase()}.{rel.ParentEntity.Name.ToCamelCase()}.{primaryField.Name.ToCamelCase()}].label?.substring(0, 25) : \"(new {CurrentEntity.FriendlyName.ToLower()})\");");
                    }
                    else
                    {
                        s.Add($"        this.breadcrumbService.changeBreadcrumb(this.route.snapshot, this.{CurrentEntity.Name.ToCamelCase()}.{rel.RelationshipFields.First().ChildField.Name.ToCamelCase()} ? this.{CurrentEntity.Name.ToCamelCase()}.{rel.ParentName.ToCamelCase()}?.{primaryField.Name.ToCamelCase() + (primaryField.JavascriptType == "string" ? "" : "?.toString()")}?.substring(0, 25) : \"(new {CurrentEntity.FriendlyName.ToLower()})\");");
                    }
                }
                else if (CurrentEntity.PrimaryField.CustomType == CustomType.Date)
                {
                    s.Add($"        this.breadcrumbService.changeBreadcrumb(this.route.snapshot, this.{CurrentEntity.Name.ToCamelCase()}.{CurrentEntity.PrimaryField.Name.ToCamelCase()} ? moment(this.{CurrentEntity.Name.ToCamelCase()}.{CurrentEntity.PrimaryField?.Name.ToCamelCase()}).format(\"LL\") : \"(new {CurrentEntity.FriendlyName.ToLower()})\");");
                }
                else if (CurrentEntity.PrimaryField.CustomType == CustomType.Enum)
                {
                    s.Add($"        this.breadcrumbService.changeBreadcrumb(this.route.snapshot, this.{CurrentEntity.Name.ToCamelCase()}.{CurrentEntity.PrimaryField.Name.ToCamelCase()} !== undefined ? Enums.{CurrentEntity.PrimaryField.Lookup.PluralName}[this.{CurrentEntity.Name.ToCamelCase()}.{CurrentEntity.PrimaryField.Name.ToCamelCase()}].label?.substring(0, 25) : \"(new {CurrentEntity.FriendlyName.ToLower()})\");");
                }
                else
                {
                    s.Add($"        this.breadcrumbService.changeBreadcrumb(this.route.snapshot, this.{CurrentEntity.Name.ToCamelCase()}.{CurrentEntity.PrimaryField.Name.ToCamelCase()} !== undefined ? this.{CurrentEntity.Name.ToCamelCase()}.{CurrentEntity.PrimaryField?.Name.ToCamelCase() + (CurrentEntity.PrimaryField?.JavascriptType == "string" ? "" : ".toString()")}.substring(0, 25) : \"(new {CurrentEntity.FriendlyName.ToLower()})\");");
                }
                s.Add($"    }}");
                s.Add($"");
            }

            if (hasFileContents)
            {
                s.Add($"    download(fileId: string): void {{");
                s.Add($"        this.downloadService.download{CurrentEntity.Name}(fileId).subscribe();");
                s.Add($"    }}");
                s.Add($"");

            }

            foreach (var rel in relationshipsAsParent)
            {
                if (!rel.DisplayListOnParent && !rel.Hierarchy) continue;

                s.Add($"    search{rel.CollectionName}(pageIndex = 0): Subject<{rel.ChildEntity.Name}SearchResponse> {{");
                s.Add($"");

                s.Add($"        this.{rel.CollectionName.ToCamelCase()}SearchOptions.pageIndex = pageIndex;");

                s.Add($"");
                s.Add($"        const subject = new Subject<{rel.ChildEntity.Name}SearchResponse>()");
                s.Add($"");
                s.Add($"        this.{rel.ChildEntity.Name.ToCamelCase()}Service.search(this.{rel.CollectionName.ToCamelCase()}SearchOptions)");
                s.Add($"            .subscribe({{");
                s.Add($"                next: response => {{");
                s.Add($"                    subject.next(response);");
                s.Add($"                    this.{rel.CollectionName.ToCamelCase()} = response.{rel.ChildEntity.PluralName.ToCamelCase()};");
                s.Add($"                    this.{rel.CollectionName.ToCamelCase()}Headers = response.headers;");
                s.Add($"                }},");
                s.Add($"                error: err => {{");
                s.Add($"                    this.errorService.handleError(err, \"{rel.CollectionFriendlyName}\", \"Load\");");
                s.Add($"                }}");
                s.Add($"            }});");
                s.Add($"");
                s.Add($"        return subject;");
                s.Add($"");
                s.Add($"    }}");
                s.Add($"");
                // todo: use relative links? can then disable 'includeParents' on these entities
                s.Add($"    goTo{rel.CollectionSingular}({rel.ChildEntity.Name.ToCamelCase()}: {rel.ChildEntity.Name}): void {{");
                s.Add($"        this.router.navigate({GetRouterLink(rel.ChildEntity, CurrentEntity)});");
                s.Add($"    }}");
                s.Add($"");
                if (rel.UseMultiSelect)
                {
                    var reverseRel = rel.ChildEntity.RelationshipsAsChild.Where(o => o.RelationshipId != rel.RelationshipId).SingleOrDefault();

                    s.Add($"    add{rel.CollectionName}(): void {{");
                    s.Add($"        this.{reverseRel.ParentEntity.Name.ToCamelCase()}Modal.open();");
                    s.Add($"    }}");
                    s.Add($"");

                    s.Add($"    change{reverseRel.ParentEntity.Name}({reverseRel.ParentEntity.PluralName.ToCamelCase()}: {reverseRel.ParentEntity.Name}[]): void {{");
                    s.Add($"        if (!{reverseRel.ParentEntity.PluralName.ToCamelCase()}.length) return;");
                    s.Add($"        const {reverseRel.RelationshipFields.First().ParentField.Name.ToCamelCase()}List = {reverseRel.ParentEntity.PluralName.ToCamelCase()}.map(o => o.{reverseRel.RelationshipFields.First().ParentField.Name.ToCamelCase()});");
                    s.Add($"        this.{CurrentEntity.Name.ToCamelCase()}Service.save{rel.ChildEntity.PluralName}({CurrentEntity.KeyFields.Select(o => $"this.{CurrentEntity.Name.ToCamelCase()}.{o.Name.ToCamelCase()}").Aggregate((current, next) => { return current + ", " + next; })}, {reverseRel.RelationshipFields.First().ParentField.Name.ToCamelCase()}List)");
                    s.Add($"            .subscribe({{");
                    s.Add($"                next: () => {{");
                    s.Add($"                    this.toastr.success(\"The {rel.ChildEntity.PluralFriendlyName.ToLower()} have been saved\", \"Save {rel.ChildEntity.PluralFriendlyName}\");");
                    if (rel.ChildEntity.HasASortField)
                        s.Add($"                    this.search{rel.CollectionName}();");
                    else
                        s.Add($"                    this.search{rel.CollectionName}(this.{rel.CollectionName.ToCamelCase()}Headers.pageIndex);");
                    s.Add($"                }},");
                    s.Add($"                error: err => {{");
                    s.Add($"                    this.errorService.handleError(err, \"{rel.ChildEntity.PluralFriendlyName}\", \"Save\");");
                    s.Add($"                }}");
                    s.Add($"            }});");
                    s.Add($"    }}");
                    s.Add($"");
                }
                s.Add($"    delete{rel.CollectionSingular}({rel.ChildEntity.Name.ToCamelCase()}: {rel.ChildEntity.Name}, event: MouseEvent): void {{");
                s.Add($"        event.stopPropagation();");
                s.Add($"");
                s.Add($"        let modalRef = this.modalService.open(ConfirmModalComponent, {{ centered: true }});");
                s.Add($"        (modalRef.componentInstance as ConfirmModalComponent).options = {{ title: \"Delete {rel.ChildEntity.FriendlyName}\", text: \"Are you sure you want to delete this {rel.ChildEntity.FriendlyName.ToLower()}?\", deleteStyle: true, ok: \"Delete\" }} as ModalOptions;");
                s.Add($"        modalRef.result.then(");
                s.Add($"            () => {{");
                s.Add($"");
                s.Add($"                this.{rel.ChildEntity.Name.ToCamelCase()}Service.delete({rel.ChildEntity.KeyFields.Select(o => $"{rel.ChildEntity.Name.ToCamelCase()}.{o.Name.ToCamelCase()}").Aggregate((current, next) => { return current + ", " + next; })})");
                s.Add($"                    .subscribe({{");
                s.Add($"                        next: () => {{");
                s.Add($"                            this.toastr.success(\"The {rel.ChildEntity.FriendlyName.ToLower()} has been deleted\", \"Delete {rel.ChildEntity.FriendlyName}\");");
                s.Add($"                            this.search{rel.CollectionName}(this.{rel.CollectionName.ToCamelCase()}Headers.pageIndex);");
                s.Add($"                        }},");
                s.Add($"                        error: err => {{");
                s.Add($"                            this.errorService.handleError(err, \"{rel.ChildEntity.FriendlyName}\", \"Delete\");");
                s.Add($"                        }}");
                s.Add($"                    }});");
                s.Add($"");
                s.Add($"            }}, () => {{ }});");
                s.Add($"    }}");
                s.Add($"");
                s.Add($"    delete{rel.CollectionName}(): void {{");
                s.Add($"        let modalRef = this.modalService.open(ConfirmModalComponent, {{ centered: true }});");
                s.Add($"        (modalRef.componentInstance as ConfirmModalComponent).options = {{ title: \"Delete {rel.ChildEntity.PluralFriendlyName}\", text: \"Are you sure you want to delete all the {rel.CollectionFriendlyName.ToLower()}?\", deleteStyle: true, ok: \"Delete\" }} as ModalOptions;");
                s.Add($"        modalRef.result.then(");
                s.Add($"            () => {{");
                s.Add($"");
                s.Add($"                this.{CurrentEntity.Name.ToCamelCase()}Service.delete{rel.CollectionName}({CurrentEntity.KeyFields.Select(o => $"this.{CurrentEntity.Name.ToCamelCase()}.{o.Name.ToCamelCase()}").Aggregate((current, next) => { return current + ", " + next; })})");
                s.Add($"                    .subscribe({{");
                s.Add($"                        next: () => {{");
                s.Add($"                            this.toastr.success(\"The {rel.CollectionFriendlyName.ToLower()} have been deleted\", \"Delete {rel.CollectionFriendlyName}\");");
                s.Add($"                            this.search{rel.CollectionName}();");
                s.Add($"                        }},");
                s.Add($"                        error: err => {{");
                s.Add($"                            this.errorService.handleError(err, \"{rel.CollectionFriendlyName}\", \"Delete\");");
                s.Add($"                        }}");
                s.Add($"                    }});");
                s.Add($"            }}, () => {{ }});");
                s.Add($"");
                s.Add($"    }}");
                s.Add($"");
            }

            foreach (var relationship in relationshipsAsParent.Where(o => o.Hierarchy && o.ChildEntity.HasASortField))
            {
                s.Add($"    show{relationship.ChildEntity.Name}Sort(): void {{");
                s.Add($"        let modalRef = this.modalService.open({relationship.ChildEntity.Name}SortComponent, {{ size: 'xl', centered: true, scrollable: false }});");
                foreach (var field in relationship.ParentEntity.KeyFields)
                {
                    var childField = relationship.RelationshipFields.Single(o => o.ParentFieldId == field.FieldId).ChildField;
                    s.Add($"        (modalRef.componentInstance as {relationship.ChildEntity.Name}SortComponent).{childField.Name.ToCamelCase()} = this.{CurrentEntity.Name.ToCamelCase()}.{field.Name.ToCamelCase()};");
                }
                s.Add($"        modalRef.result.then(");
                s.Add($"            () => this.search{relationship.CollectionName}(this.{relationship.CollectionName.ToCamelCase()}Headers.pageIndex),");
                s.Add($"            () => {{ }}");
                s.Add($"        );");
                s.Add($"    }}");
                s.Add($"");
            }

            s.Add($"}}");

            return RunCodeReplacements(s.ToString(), CodeType.EditTypeScript);

        }
    }
}