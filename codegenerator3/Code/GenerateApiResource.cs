using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace WEB.Models
{
    public partial class Code
    {
        public string GenerateApiResource()
        {
            var s = new StringBuilder();

            var noKeysEntity = NormalEntities.FirstOrDefault(e => e.KeyFields.Count == 0);
            if (noKeysEntity != null)
                throw new InvalidOperationException(noKeysEntity.FriendlyName + " has no keys defined");

            s.Add($"import {{ environment }} from '../../../environments/environment';");
            s.Add($"import {{ Injectable }} from '@angular/core';");
            s.Add($"import {{ HttpClient{(CurrentEntity.EntityType != EntityType.Settings ? ", HttpParams" : "")} }} from '@angular/common/http';");
            s.Add($"import {{ Observable }} from 'rxjs';");

            if (CurrentEntity.EntityType != EntityType.Settings)
                s.Add($"import {{ map }} from 'rxjs/operators';");
            s.Add($"import {{ {CurrentEntity.Name}{(CurrentEntity.EntityType != EntityType.Settings ? $", {CurrentEntity.Name}SearchOptions, {CurrentEntity.Name}SearchResponse" : "")} }} from '../models/{CurrentEntity.Name.ToLower()}.model';");
            if (CurrentEntity.EntityType != EntityType.Settings)
                s.Add($"import {{ SearchQuery, PagingHeaders }} from '../models/http.model';");
            if (CurrentEntity.KeyFields.Any(f => f.CustomType == CustomType.Date))
                s.Add($"import * as moment from 'moment';");

            s.Add($"");
            s.Add($"@Injectable({{ providedIn: 'root' }})");
            s.Add($"export class {CurrentEntity.Name}Service{(CurrentEntity.EntityType != EntityType.Settings ? " extends SearchQuery" : "")} {{");
            s.Add($"");

            s.Add($"    constructor(private http: HttpClient) {{");
            if (CurrentEntity.EntityType != EntityType.Settings)
                s.Add($"        super();");
            s.Add($"    }}");
            s.Add($"");

            if (CurrentEntity.EntityType != EntityType.Settings)
            {
                s.Add($"    search(params: {CurrentEntity.Name}SearchOptions): Observable<{CurrentEntity.Name}SearchResponse> {{");
                s.Add($"        const queryParams: HttpParams = this.buildQueryParams(params);");
                s.Add($"        return this.http.get(`${{environment.baseApiUrl}}{CurrentEntity.PluralName.ToLower()}`, {{ params: queryParams, observe: 'response' }})");
                s.Add($"            .pipe(");
                s.Add($"                map(response => {{");
                s.Add($"                    const headers = JSON.parse(response.headers.get(\"x-pagination\")) as PagingHeaders;");
                s.Add($"                    const {CurrentEntity.PluralName.ToCamelCase()} = response.body as {CurrentEntity.Name}[];");
                s.Add($"                    return {{ {CurrentEntity.PluralName.ToCamelCase()}: {CurrentEntity.PluralName.ToCamelCase()}, headers: headers }};");
                s.Add($"                }})");
                s.Add($"            );");
                s.Add($"    }}");
                s.Add($"");
            }

            var getParams = CurrentEntity.KeyFields.Select(o => o.Name.ToCamelCase() + ": " + o.JavascriptType).Aggregate((current, next) => current + ", " + next);
            var saveParams = CurrentEntity.Name.ToCamelCase() + ": " + CurrentEntity.Name;
            var getUrl = "/" + CurrentEntity.KeyFields.Select(o => "${" + (o.CustomType == CustomType.Date ? $"moment({o.Name.ToCamelCase()}).toISOString()" : o.Name.ToCamelCase()) + "}").Aggregate((current, next) => current + "/" + next);
            var saveUrl = "/" + CurrentEntity.KeyFields.Select(o => "${" + (o.CustomType == CustomType.Date ? $"moment({CurrentEntity.Name.ToCamelCase() + "." + o.Name.ToCamelCase()}).toISOString()" : CurrentEntity.Name.ToCamelCase() + "." + o.Name.ToCamelCase() + (o.FieldType == FieldType.Int && CurrentEntity.KeyFields.Count() == 1 ? " ?? 0" : "")) + "}").Aggregate((current, next) => current + "/" + next);

            if (CurrentEntity.EntityType == EntityType.Settings)
            {
                getParams = string.Empty;
                getUrl = string.Empty;
                saveUrl = string.Empty;
            }

            s.Add($"    get({getParams}): Observable<{CurrentEntity.Name}> {{");
            s.Add($"        return this.http.get<{CurrentEntity.Name}>(`${{environment.baseApiUrl}}{CurrentEntity.PluralName.ToLower()}{getUrl}`);");
            s.Add($"    }}");
            s.Add($"");

            s.Add($"    save({saveParams}): Observable<{CurrentEntity.Name}> {{");
            s.Add($"        return this.http.post<{CurrentEntity.Name}>(`${{environment.baseApiUrl}}{CurrentEntity.PluralName.ToLower()}{saveUrl}`, {CurrentEntity.Name.ToCamelCase()});");
            s.Add($"    }}");
            s.Add($"");

            if (CurrentEntity.EntityType != EntityType.Settings)
            {
                s.Add($"    delete({getParams}): Observable<void> {{");
                s.Add($"        return this.http.delete<void>(`${{environment.baseApiUrl}}{CurrentEntity.PluralName.ToLower()}{getUrl}`);");
                s.Add($"    }}");
                s.Add($"");
            }

            if (CurrentEntity.HasASortField)
            {
                var relHierarchy = CurrentEntity.RelationshipsAsChild.SingleOrDefault(o => o.Hierarchy);
                if (relHierarchy != null)
                {
                    var sortParams = relHierarchy.RelationshipFields.Select(o => $"{o.ChildField.Name.ToCamelCase()}: {o.ChildField.JavascriptType}").Aggregate((current, next) => current + ", " + next);
                    s.Add($"    sort({sortParams}, ids: {CurrentEntity.KeyFields.First().JavascriptType}[]): Observable<void> {{");
                    var sortQuery = relHierarchy.RelationshipFields.Select(o => $"{o.ChildField.Name.ToLower()}=${{{o.ChildField.Name.ToCamelCase()}}}").Aggregate((current, next) => current + "&" + next);
                    s.Add($"        return this.http.post<void>(`${{environment.baseApiUrl}}{CurrentEntity.PluralName.ToLower()}/sort?{sortQuery}`, ids);");
                }
                else
                {
                    s.Add($"    sort(ids: {CurrentEntity.KeyFields.First().JavascriptType}[]): Observable<void> {{");
                    s.Add($"        return this.http.post<void>(`${{environment.baseApiUrl}}{CurrentEntity.PluralName.ToLower()}/sort`, ids);");
                }
                s.Add($"    }}");
                s.Add($"");
            }

            var processedEntities = new List<Guid>();
            foreach (var rel in CurrentEntity.RelationshipsAsParent.Where(o => o.UseMultiSelect && !o.ChildEntity.Exclude))
            {
                if (processedEntities.Contains(rel.ChildEntity.EntityId)) continue;
                processedEntities.Add(rel.ChildEntity.EntityId);

                var reverseRel = rel.ChildEntity.RelationshipsAsChild.Where(o => o.RelationshipId != rel.RelationshipId).SingleOrDefault();

                s.Add($"    save{rel.ChildEntity.PluralName}({rel.RelationshipFields.First().ParentField.Name.ToCamelCase()}: {rel.RelationshipFields.First().ParentField.JavascriptType}, {reverseRel.RelationshipFields.First().ParentField.Name.ToCamelCase()}s: {reverseRel.RelationshipFields.First().ParentField.JavascriptType}[]): Observable<void> {{");
                s.Add($"        return this.http.post<void>(`${{environment.baseApiUrl}}{CurrentEntity.PluralName.ToLower()}{getUrl}/{rel.ChildEntity.PluralName.ToLower()}`, {reverseRel.RelationshipFields.First().ParentField.Name.ToCamelCase()}s);");
                s.Add($"    }}");
                s.Add($"");
            }

            foreach (var rel in CurrentEntity.RelationshipsAsParent.Where(r => !r.ChildEntity.Exclude && r.DisplayListOnParent).OrderBy(r => r.SortOrder))
            {
                s.Add($"    delete{rel.CollectionName}({rel.RelationshipFields.First().ParentField.Name.ToCamelCase()}: {rel.RelationshipFields.First().ParentField.JavascriptType}): Observable<void> {{");
                s.Add($"        return this.http.delete<void>(`${{environment.baseApiUrl}}{CurrentEntity.PluralName.ToLower()}{getUrl}/{rel.CollectionName.ToLower()}`);");
                s.Add($"    }}");
                s.Add($"");
            }
            s.Add($"}}");

            return RunCodeReplacements(s.ToString(), CodeType.ApiResource);

        }
    }
}