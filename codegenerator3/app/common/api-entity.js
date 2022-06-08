(function () {
    "use strict";
    angular
        .module("app")
        .factory("codeReplacementResource", codeReplacementResource)
        .factory("entityResource", entityResource)
        .factory("fieldResource", fieldResource)
        .factory("lookupResource", lookupResource)
        .factory("lookupOptionResource", lookupOptionResource)
        .factory("projectResource", projectResource)
        .factory("relationshipResource", relationshipResource)
        .factory("relationshipFieldResource", relationshipFieldResource);
    codeReplacementResource.$inject = ["$resource", "appSettings"];
    function codeReplacementResource($resource, appSettings) {
        return $resource(appSettings.apiServiceBaseUri + appSettings.apiPrefix + "codereplacements/:codeReplacementId", {
            codeReplacementId: "@codeReplacementId"
        }, {
            sort: {
                method: "POST",
                url: appSettings.apiServiceBaseUri + appSettings.apiPrefix + "codereplacements/sort"
            }
        });
    }
    entityResource.$inject = ["$resource", "appSettings"];
    function entityResource($resource, appSettings) {
        return $resource(appSettings.apiServiceBaseUri + appSettings.apiPrefix + "entities/:entityId", {
            entityId: "@entityId"
        }, {
            generate: {
                method: "GET",
                url: appSettings.apiServiceBaseUri + appSettings.apiPrefix + "entities/:entityId/code",
                isArray: false
            },
            deploy: {
                method: "POST",
                url: appSettings.apiServiceBaseUri + appSettings.apiPrefix + "entities/:entityId/code",
                isArray: false
            },
            reorderFields: {
                method: "POST",
                url: appSettings.apiServiceBaseUri + appSettings.apiPrefix + "entities/:entityId/reorderfields"
            }
        });
    }
    fieldResource.$inject = ["$resource", "appSettings"];
    function fieldResource($resource, appSettings) {
        return $resource(appSettings.apiServiceBaseUri + appSettings.apiPrefix + "fields/:fieldId", {
            fieldId: "@fieldId"
        }, {
            sort: {
                method: "POST",
                url: appSettings.apiServiceBaseUri + appSettings.apiPrefix + "fields/sort"
            }
        });
    }
    lookupResource.$inject = ["$resource", "appSettings"];
    function lookupResource($resource, appSettings) {
        return $resource(appSettings.apiServiceBaseUri + appSettings.apiPrefix + "lookups/:lookupId", {
            lookupId: "@lookupId"
        }, {
            updateOrders: {
                method: "POST",
                url: appSettings.apiServiceBaseUri + appSettings.apiPrefix + "lookups/:lookupId/updateorders"
            }
        });
    }
    lookupOptionResource.$inject = ["$resource", "appSettings"];
    function lookupOptionResource($resource, appSettings) {
        return $resource(appSettings.apiServiceBaseUri + appSettings.apiPrefix + "lookupoptions/:lookupOptionId", {
            lookupOptionId: "@lookupOptionId"
        });
    }
    projectResource.$inject = ["$resource", "appSettings"];
    function projectResource($resource, appSettings) {
        return $resource(appSettings.apiServiceBaseUri + appSettings.apiPrefix + "projects/:projectId", {
            projectId: "@projectId"
        });
    }
    relationshipResource.$inject = ["$resource", "appSettings"];
    function relationshipResource($resource, appSettings) {
        return $resource(appSettings.apiServiceBaseUri + appSettings.apiPrefix + "relationships/:relationshipId", {
            relationshipId: "@relationshipId"
        }, {
            sort: {
                method: "POST",
                url: appSettings.apiServiceBaseUri + appSettings.apiPrefix + "relationships/sort"
            },
            sortChild: {
                method: "POST",
                url: appSettings.apiServiceBaseUri + appSettings.apiPrefix + "relationships/sortchild"
            }
        });
    }
    relationshipFieldResource.$inject = ["$resource", "appSettings"];
    function relationshipFieldResource($resource, appSettings) {
        return $resource(appSettings.apiServiceBaseUri + appSettings.apiPrefix + "relationshipfields/:relationshipFieldId", {
            relationshipFieldId: "@relationshipFieldId"
        });
    }
}());
//# sourceMappingURL=api-entity.js.map