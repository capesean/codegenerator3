(function () {
    "use strict";
    angular
        .module("app")
        .controller("settings", settings);
    settings.$inject = ["$scope", "$state", "$stateParams", "settingsResource", "notifications", "appSettings", "errorService"];
    function settings($scope, $state, $stateParams, settingsResource, notifications, appSettings, errorService) {
        var vm = this;
        vm.loading = true;
        vm.settings = new settingsResource();
        vm.save = saveSettings;
        initPage();
        function initPage() {
            vm.loading = false;
        }
        function saveSettings() {
            if ($scope.mainForm.$invalid) {
                notifications.error("The form has not been completed correctly.", "Error");
            }
            else {
                vm.loading = true;
                vm.settings.$save(function (data) {
                    vm.client = data;
                    notifications.success("The settings has been saved.", "Saved");
                }, function (err) {
                    errorService.handleApiError(err, "settings");
                })
                    .finally(function () { return vm.loading = false; });
            }
        }
        ;
    }
    ;
}());
//# sourceMappingURL=settings.js.map