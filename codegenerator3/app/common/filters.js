(function () {
    "use strict";
    angular
        .module("app")
        .filter("yesNo", function () { return function (text) { return text ? "Yes" : "No"; }; });
})();
//# sourceMappingURL=filters.js.map