(function () {
    "use strict";
    angular
        .module("app")
        .factory("authService", authService)
        .factory("authorization", authorization);
    authService.$inject = ["$http", "$q", "$rootScope", "appSettings"];
    function authService($http, $q, $rootScope, appSettings) {
        var service = {
            isInRole: isInRole,
            isInAnyRole: isInAnyRole
        };
        return service;
        function isInRole(roleName) {
            if (!$rootScope.identity || !$rootScope.identity.roles)
                return false;
            var roleId;
            angular.forEach(appSettings.roles, function (role) {
                if (role.name === roleName)
                    roleId = role.id;
            });
            if (!roleId)
                return false;
            var found = false;
            angular.forEach($rootScope.identity.roles, function (role) {
                if (role.roleId === roleId)
                    found = true;
            });
            return found;
        }
        function isInAnyRole(roles) {
            if (!$rootScope.identity || !$rootScope.identity.roles)
                return false;
            for (var i = 0; i < roles.length; i++) {
                if (this.isInRole(roles[i]))
                    return true;
            }
            return false;
        }
    }
    authorization.$inject = ["$rootScope", "$state", "authService", "notifications", "$window"];
    function authorization($rootScope, $state, authService, notifications, $window) {
        return {
            authorize: function () {
                return authService.getIdentity()
                    .then(function () {
                    var isLoggedIn = authService.isLoggedIn();
                    $rootScope.toState = $rootScope.toState || {};
                    $rootScope.toState.data = $rootScope.toState.data || {};
                    if (!isLoggedIn) {
                        alert("not logged in");
                    }
                    else {
                        if ($rootScope.toState.data.allowAny) {
                        }
                        else if (Object.prototype.toString.call($rootScope.toState.data.roles) !== "[object Array]") {
                            notifications.error("Route does not have any permissions - use allowAny if applicable");
                            throw ("Route does not have any permissions");
                        }
                        else if (!authService.isInAnyRole($rootScope.toState.data.roles)) {
                            $state.go("app.accessdenied");
                        }
                    }
                    ;
                });
            }
        };
    }
}());
//# sourceMappingURL=authservice.js.map