(function () {
    "use strict";
    angular
        .module("app")
        .controller("projects", projects);
    projects.$inject = ["$scope", "$state", "$q", "$timeout", "notifications", "appSettings", "projectResource"];
    function projects($scope, $state, $q, $timeout, notifications, appSettings, projectResource) {
        var vm = this;
        vm.loading = true;
        vm.appSettings = appSettings;
        vm.search = { pageSize: 0 };
        vm.runSearch = runSearch;
        vm.goToProject = function (projectId) { return $state.go("app.project", { projectId: projectId }); };
        vm.moment = moment;
        initPage();
        function initPage() {
            var promises = [];
            $q.all(promises).finally(function () { return runSearch(0); });
        }
        function runSearch(pageIndex) {
            vm.loading = true;
            vm.search.pageIndex = pageIndex;
            var promises = [];
            promises.push(projectResource.query(vm.search, function (data, headers) {
                vm.projects = data;
                vm.headers = JSON.parse(headers("X-Pagination"));
            }, function (err) {
                notifications.error("Failed to load the projects.", "Error", err);
                $state.go("app.home");
            }).$promise);
            $q.all(promises).finally(function () { return vm.loading = false; });
        }
        ;
    }
    ;
}());
//# sourceMappingURL=projects.js.map