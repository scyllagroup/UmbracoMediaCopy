angular.module('umbraco.resources').factory('mediaExtendedResource', function ($q, $http) {
    //the factory object returned
    return {
        //call the RetailerApi Controller that we created on the backend under the controllers folder
        copy: function (params) {
            var dfrd = $.Deferred();

            $http({ url: "backoffice/Website/MediaExtensionsApi/Copy", method: "POST", params: params })
                .success(function (result) { dfrd.resolve(result); })
                .error(function (result) { dfrd.resolve(result); });

            return dfrd.promise();
        },

        regenerateCrops: function(){
            var dfrd = $.Deferred();

            $http({ url: "backoffice/Website/MediaExtensionsApi/RegenerateCrops", method: "POST" })
                .success(function (result) { dfrd.resolve(result); })
                .error(function (result) { dfrd.resolve(result); });

            return dfrd.promise();
        }
    }
});
