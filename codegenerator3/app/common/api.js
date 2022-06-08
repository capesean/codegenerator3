(function () {
    "use strict";
    angular
        .module("app")
        .factory("settingsResource", settingsResource)
        .factory("userResource", userResource)
        .factory("utilitiesResource", utilitiesResource);
    settingsResource.$inject = ["$resource", "appSettings"];
    function settingsResource($resource, appSettings) {
        return $resource(appSettings.apiServiceBaseUri + appSettings.apiPrefix + "settings");
    }
    userResource.$inject = ["$resource", "appSettings"];
    function userResource($resource, appSettings) {
        return $resource(appSettings.apiServiceBaseUri + appSettings.apiPrefix + "users/:id", { id: "@id" }, {
            profile: {
                method: "GET",
                url: appSettings.apiServiceBaseUri + appSettings.apiPrefix + "users/:id/profile"
            }
        });
    }
    utilitiesResource.$inject = ["$resource", "appSettings"];
    function utilitiesResource($resource, appSettings) {
        return $resource(appSettings.apiServiceBaseUri + appSettings.apiPrefix + "utilities", {}, {
            multiDeploy: {
                method: "POST",
                url: appSettings.apiServiceBaseUri + appSettings.apiPrefix + "utilities/multideploy",
                isArray: true
            }
        });
    }
}());
//# sourceMappingURL=api.js.map