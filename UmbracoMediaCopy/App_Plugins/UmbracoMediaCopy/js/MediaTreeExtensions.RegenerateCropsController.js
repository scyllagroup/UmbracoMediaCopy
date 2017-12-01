angular.module("umbraco").controller("MediaTreeExtensions.RegenerateCropsController",
	function ($scope, eventsService, mediaExtendedResource, navigationService, appState, treeService, localizationService) {

	    var dialogOptions = $scope.dialogOptions;

	    $scope.busy = false;
	    $scope.success = false;
	    $scope.error = false;

	    $scope.regenerateCrops = function () {

	        $scope.busy = true;
	        $scope.error = false;

	        mediaExtendedResource.regenerateCrops()
                .then(function (result) {

                    if (result.success) {
                        $scope.error = false;
                        $scope.success = true;
                        $scope.busy = false;

                        //we need to do a double sync here: first sync to the copied content - but don't activate the node,
                        //then sync to the currenlty edited content (note: this might not be the content that was copied!!)

                        navigationService.syncTree({ tree: "media", path: result.path, forceReload: true, activate: false }).then(function (args) {
                            if (activeNode) {
                                var activeNodePath = treeService.getPath(activeNode).join();
                                //sync to this node now - depending on what was copied this might already be synced but might not be
                                navigationService.syncTree({ tree: "media", path: activeNodePath, forceReload: false, activate: true });
                            }
                        });
                    } else {
                        $scope.error = { errorMsg: "Error Regenerating Crops", data: { Message: result.message } };
                        $scope.success = false;
                        $scope.busy = false;
                    }

                }, function (err) {
                    $scope.success = false;
                    $scope.error = err;
                    $scope.busy = false;
                });
	    };
	});