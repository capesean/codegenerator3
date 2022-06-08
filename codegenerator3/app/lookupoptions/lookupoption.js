(function () {
    "use strict";
    angular
        .module("app")
        .controller("lookupOption", lookupOption);
    lookupOption.$inject = ["$scope", "$state", "$stateParams", "notifications", "appSettings", "$q", "errorService", "lookupOptionResource", "lookupResource"];
    function lookupOption($scope, $state, $stateParams, notifications, appSettings, $q, errorService, lookupOptionResource, lookupResource) {
        var vm = this;
        vm.loading = true;
        vm.appSettings = appSettings;
        vm.save = save;
        vm.delete = del;
        vm.isNew = $stateParams.lookupOptionId === vm.appSettings.newGuid;
        initPage();
        function initPage() {
            var promises = [];
            if (vm.isNew) {
                vm.lookupOption = new lookupOptionResource();
                vm.lookupOption.lookupOptionId = appSettings.newGuid;
                vm.lookupOption.lookupId = $stateParams.lookupId;
                vm.lookupOption.sortOrder = 0;
                promises.push(lookupResource.get({
                    lookupId: $stateParams.lookupId
                }, function (data) {
                    vm.lookup = data;
                    vm.project = vm.lookup.project;
                }, function (err) {
                    errorService.handleApiError(err, "lookup", "load");
                    $state.go("app.lookup", { projectId: $stateParams.projectId, lookupId: $stateParams.lookupId });
                }).$promise);
            }
            else {
                promises.push(lookupOptionResource.get({
                    lookupOptionId: $stateParams.lookupOptionId
                }, function (data) {
                    vm.lookupOption = data;
                    vm.lookup = vm.lookupOption.lookup;
                    vm.project = vm.lookup.project;
                }, function (err) {
                    errorService.handleApiError(err, "lookup option", "load");
                    $state.go("app.lookup", { projectId: $stateParams.projectId, lookupId: $stateParams.lookupId });
                }).$promise);
            }
            $q.all(promises).finally(function () { return vm.loading = false; });
        }
        function save() {
            if ($scope.mainForm.$invalid) {
                notifications.error("The form has not been completed correctly.", "Error");
                return;
            }
            vm.loading = true;
            vm.lookupOption.$save(function (data) {
                notifications.success("The lookup option has been saved.", "Saved");
                $state.go("app.lookup", { projectId: $stateParams.projectId, lookupId: $stateParams.lookupId });
            }, function (err) {
                errorService.handleApiError(err, "lookup option");
            }).finally(function () { return vm.loading = false; });
        }
        function del() {
            if (!confirm("Confirm delete?"))
                return;
            vm.loading = true;
            lookupOptionResource.delete({
                lookupOptionId: $stateParams.lookupOptionId
            }, function () {
                notifications.success("The lookup option has been deleted.", "Deleted");
                $state.go("app.lookup", { projectId: $stateParams.projectId, lookupId: $stateParams.lookupId });
            }, function (err) {
                errorService.handleApiError(err, "lookup option", "delete");
            })
                .$promise.finally(function () { return vm.loading = false; });
        }
    }
    ;
}());
//# sourceMappingURL=lookupoption.js.map