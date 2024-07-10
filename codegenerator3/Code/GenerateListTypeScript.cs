using System.Linq;
using System.Text;

namespace WEB.Models
{
    public partial class Code
    {
        public string GenerateListTypeScript()
        {
            var folders = string.Join("", Enumerable.Repeat("../", CurrentEntity.Project.GeneratedPath.Count(o => o == '/')));

            bool includeParents = false;
            if (CurrentEntity.RelationshipsAsChild.Any(r => r.Hierarchy))
                includeParents = true;
            else
                foreach (var field in CurrentEntity.Fields.Where(f => f.ShowInSearchResults).OrderBy(f => f.FieldOrder))
                    if (CurrentEntity.RelationshipsAsChild.Any(r => r.RelationshipFields.Any(f => f.ChildFieldId == field.FieldId)))
                    {
                        includeParents = true;
                        break;
                    }
            var enumLookups = CurrentEntity.Fields.Where(o => o.FieldType == FieldType.Enum && (o.ShowInSearchResults || o.SearchType == SearchType.Exact)).Select(o => o.Lookup).Distinct().ToList();
            var relationshipsAsParent = CurrentEntity.RelationshipsAsParent.Where(r => !r.ChildEntity.Exclude && r.DisplayListOnParent && r.Hierarchy).OrderBy(r => r.SortOrder);
            var hasChildRoutes = relationshipsAsParent.Any();
            if (!CurrentEntity.RelationshipsAsChild.Any(o => o.Hierarchy && !o.ParentEntity.Exclude))
                hasChildRoutes = true;

            var s = new StringBuilder();

            s.Add($"import {{ Component, OnInit{(hasChildRoutes ? ", OnDestroy" : "")} }} from '@angular/core';");
            s.Add($"import {{ Router, ActivatedRoute{(hasChildRoutes ? ", NavigationEnd" : "")} }} from '@angular/router';");
            s.Add($"import {{ Subject{(hasChildRoutes ? ", Subscription" : "")} }} from 'rxjs';");
            if (CurrentEntity.HasASortField)
            {
                s.Add($"import {{ ToastrService }} from 'ngx-toastr';");
                s.Add($"import {{ NgbModal }} from '@ng-bootstrap/ng-bootstrap';");
            }


            s.Add($"import {{ PagingHeaders }} from '{folders}../common/models/http.model';");
            s.Add($"import {{ {CurrentEntity.Name}SearchOptions, {CurrentEntity.Name}SearchResponse, {CurrentEntity.Name} }} from '{folders}../common/models/{CurrentEntity.Name.ToLower()}.model';");
            if (enumLookups.Any())
                s.Add($"import {{ Enum, Enums }} from '{folders}../common/models/enums.model';");
            if (CurrentEntity.EntityType == EntityType.User && !enumLookups.Any())
                s.Add($"import {{ Enums }} from '{folders}../common/models/enums.model';");

            s.Add($"import {{ ErrorService }} from '{folders}../common/services/error.service';");
            s.Add($"import {{ {CurrentEntity.Name}Service }} from '{folders}../common/services/{CurrentEntity.Name.ToLower()}.service';");

            if (CurrentEntity.HasASortField)
                s.Add($"import {{ {CurrentEntity.Name}SortComponent }} from './{CurrentEntity.Name.ToLower()}.sort.component';");

            if (CurrentEntity.HasAFileContentsField)
                s.Add($"import {{ DownloadService }} from '../common/services/download.service';");

            s.Add($"");
            s.Add($"@Component({{");
            s.Add($"    selector: '{CurrentEntity.Name.ToLower()}-list',");
            s.Add($"    templateUrl: './{CurrentEntity.Name.ToLower()}.list.component.html'");
            s.Add($"}})");
            s.Add($"export class {CurrentEntity.Name}ListComponent implements OnInit{(hasChildRoutes ? ", OnDestroy" : "")} {{");
            s.Add($"");
            s.Add($"    public {CurrentEntity.PluralName.ToCamelCase()}: {CurrentEntity.Name}[] = [];");
            s.Add($"    public searchOptions = new {CurrentEntity.Name}SearchOptions();");
            s.Add($"    public headers = new PagingHeaders();");
            if (hasChildRoutes)
                s.Add($"    private routerSubscription: Subscription;");
            foreach (var enumLookup in enumLookups)
                s.Add($"    public {enumLookup.PluralName.ToCamelCase()}: Enum[] = Enums.{enumLookup.PluralName};");
            if (CurrentEntity.EntityType == EntityType.User)
                s.Add($"    public roles = Enums.Roles;");

            s.Add($"");
            s.Add($"    constructor(");
            s.Add($"        public route: ActivatedRoute,");
            s.Add($"        private router: Router,");
            s.Add($"        private errorService: ErrorService,");
            if (CurrentEntity.HasASortField)
                s.Add($"        private modalService: NgbModal,");
            if (CurrentEntity.HasAFileContentsField)
                s.Add($"        private downloadService: DownloadService,");
            s.Add($"        private {CurrentEntity.Name.ToCamelCase()}Service: {CurrentEntity.Name}Service");
            s.Add($"    ) {{");
            s.Add($"    }}");
            s.Add($"");
            s.Add($"    ngOnInit(): void {{");
            if (includeParents)
                s.Add($"        this.searchOptions.includeParents = true;");
            if (hasChildRoutes)
            {
                s.Add($"        this.routerSubscription = this.router.events.subscribe(event => {{");
                s.Add($"            if (event instanceof NavigationEnd && !this.route.firstChild) {{");
                s.Add($"                this.runSearch();");
                s.Add($"            }}");
                s.Add($"        }});");
            }
            s.Add($"        this.runSearch();");
            s.Add($"    }}");
            s.Add($"");
            if (hasChildRoutes)
            {
                s.Add($"    ngOnDestroy(): void {{");
                s.Add($"        this.routerSubscription.unsubscribe();");
                s.Add($"    }}");
                s.Add($"");
            }
            s.Add($"    runSearch(pageIndex = 0{(CurrentEntity.HasSortableFields ? ", orderBy: string = null" : "")}): Subject<{CurrentEntity.Name}SearchResponse> {{");
            s.Add($"");
            s.Add($"        this.searchOptions.pageIndex = pageIndex;");
            s.Add($"");

            if (CurrentEntity.HasSortableFields)
            {
                s.Add($"        if (orderBy != null) {{");
                s.Add($"            if (this.searchOptions.orderBy === orderBy)");
                s.Add($"                this.searchOptions.orderByAscending = this.searchOptions.orderByAscending == null ? true : !this.searchOptions.orderByAscending;");
                s.Add($"            else {{");
                s.Add($"                this.searchOptions.orderBy = orderBy;");
                s.Add($"                this.searchOptions.orderByAscending = true;");
                s.Add($"            }}");
                s.Add($"        }}");
                s.Add($"");
            }

            s.Add($"        const subject = new Subject<{CurrentEntity.Name}SearchResponse>();");
            s.Add($"");
            s.Add($"        this.{CurrentEntity.Name.ToCamelCase()}Service.search(this.searchOptions)");
            s.Add($"            .subscribe({{");
            s.Add($"                next: response => {{");
            s.Add($"                    subject.next(response);");
            s.Add($"                    this.{CurrentEntity.PluralName.ToCamelCase()} = response.{CurrentEntity.PluralName.ToCamelCase()};");
            s.Add($"                    this.headers = response.headers;");
            s.Add($"                }},");
            s.Add($"                error: err => {{");
            s.Add($"                    this.errorService.handleError(err, \"{CurrentEntity.PluralFriendlyName}\", \"Load\");");
            s.Add($"                }}");
            s.Add($"            }});");
            s.Add($"");
            s.Add($"        return subject;");
            s.Add($"");
            s.Add($"    }}");
            s.Add($"");
            if (CurrentEntity.HasASortField)
            {
                s.Add($"    showSort(): void {{");
                s.Add($"        let modalRef = this.modalService.open({CurrentEntity.Name}SortComponent, {{ size: 'xl', centered: true, scrollable: true }});");
                s.Add($"        modalRef.result.then(");
                s.Add($"            () => {{");
                s.Add($"");
                s.Add($"                this.searchOptions.orderBy = null;");
                s.Add($"                this.runSearch(this.headers.pageIndex);");
                s.Add($"");
                s.Add($"            }}, () => {{ }});");
                s.Add($"    }}");
                s.Add($"");
            }
            if (CurrentEntity.HasAFileContentsField)
            {
                s.Add($"    download{CurrentEntity.Name}({CurrentEntity.Name.ToCamelCase()}: {CurrentEntity.Name}, event: MouseEvent) {{");
                s.Add($"        event.stopPropagation();");
                s.Add($"");
                s.Add($"        this.downloadService.download{CurrentEntity.Name}({CurrentEntity.KeyFields.Select(o => $"{CurrentEntity.Name.ToCamelCase()}.{o.Name.ToCamelCase()}").Aggregate((current, next) => { return current + ", " + next; })}).subscribe();");
                s.Add($"    }}");
                s.Add($"");
            }
            s.Add($"    goTo{CurrentEntity.Name}({CurrentEntity.Name.ToCamelCase()}: {CurrentEntity.Name}): void {{");
            s.Add($"        this.router.navigate({GetRouterLink(CurrentEntity, CurrentEntity)});");
            s.Add($"    }}");
            s.Add($"}}");
            s.Add($"");

            return RunCodeReplacements(s.ToString(), CodeType.ListTypeScript);
        }
    }
}