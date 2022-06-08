/// <reference path="../../scripts/typings/angularjs/angular.d.ts" />
(function () {
    "use strict";

    angular
        .module("app")
        .controller("codeReplacements", codeReplacements);

    codeReplacements.$inject = ["$scope", "$state", "$q", "$timeout", "notifications", "appSettings", "codeReplacementResource"];
    function codeReplacements($scope, $state, $q, $timeout, notifications, appSettings, codeReplacementResource) {

        var vm = this;
        vm.loading = true;
        vm.appSettings = appSettings;
        vm.search = { };
        vm.searchObjects = { };
        vm.runSearch = runSearch;
        vm.goToCodeReplacement = (projectId, entityId, codeReplacementId) => $state.go("app.codeReplacement", { projectId: projectId, entityId: entityId, codeReplacementId: codeReplacementId });
        vm.sortOptions = { stop: sortItems, handle: "i.sortable-handle", axis: "y" };
        vm.moment = moment;

        initPage();

        function initPage() {

            var promises = [];

            $q.all(promises).finally(() => runSearch(0));

        }

        function runSearch(pageIndex) {

            vm.loading = true;

            vm.search.includeEntities = true;
            vm.search.pageSize = 0;

            var promises = [];

            promises.push(
                codeReplacementResource.query(
                    vm.search,
                    (data, headers) => {

                        vm.codeReplacements = data;

                    },
                    err => {

                        notifications.error("Failed to load the code replacements.", "Error", err);
                        $state.go("app.home");

                    }).$promise
            );

            $q.all(promises).finally(() => vm.loading = false);

        };

        function sortItems() {

            vm.loading = true;

            var ids = [];
            angular.forEach(vm.codeReplacements, function (item, index) {
                ids.push(item.codeReplacementId);
            });

            codeReplacementResource.sort(
                {
                    ids: ids
                },
                data => {

                    notifications.success("The sort order has been updated", "Code Replacement Ordering");

                },
                err => {

                    notifications.error("Failed to sort the code replacements. " + (err.data && err.data.message ? err.data.message : ""), "Error", err);

                })
                .$promise.finally(() => vm.loading = false);

        }

    };
} ());
