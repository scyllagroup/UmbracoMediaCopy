angular.module("umbraco").controller("MediaTreeExtensions.CopyController",
	function ($scope, eventsService, mediaExtendedResource, navigationService, appState, treeService, localizationService) {

	    var dialogOptions = $scope.dialogOptions;
	   

	    $scope.copyChildren = false;
	    $scope.dialogTreeEventHandler = $({});
	    $scope.busy = false;
	    

	    var node = dialogOptions.currentNode;

	    function nodeSelectHandler(ev, args) {
	        args.event.preventDefault();
	        args.event.stopPropagation();

            
	        if (args.node.metaData.listViewNode) {
	            //check if list view 'search' node was selected

	            $scope.searchInfo.showSearch = true;
	            $scope.searchInfo.searchFromId = args.node.metaData.listViewNode.id;
	            $scope.searchInfo.searchFromName = args.node.metaData.listViewNode.name;
	        }
	        else {
	            eventsService.emit("editors.content.copyController.select", args);

	            if ($scope.target) {
	                //un-select if there's a current one selected
	                $scope.target.selected = false;
	            }

	            $scope.target = args.node;
	            $scope.target.selected = true;
	        }

	    }

	    function nodeExpandedHandler(ev, args) {
	        if (angular.isArray(args.children)) {

	            //iterate children
	            _.each(args.children, function (child) {
	                //check if any of the items are list views, if so we need to add a custom 
	                // child: A node to activate the search
	                if (child.metaData.isContainer) {
	                    child.hasChildren = true;
	                    child.children = [
	                        {
	                            level: child.level + 1,
	                            hasChildren: false,
	                            name: searchText,
	                            metaData: {
	                                listViewNode: child,
	                            },
	                            cssClass: "icon umb-tree-icon sprTree icon-search",
	                            cssClasses: ["not-published"]
	                        }
	                    ];
	                }
	            });
	        }
	    }




	    $scope.copy = function () {

	        $scope.busy = true;
	        $scope.error = false;

	        mediaExtendedResource.copy({ parentId: $scope.target.id, nodeId: node.id, copyChildren: $scope.copyChildren })
                .then(function (result) {

                    if (result.success) {
                        $scope.error = false;
                        $scope.success = true;
                        $scope.busy = false;

                        //get the currently edited node (if any)
                        var activeNode = appState.getTreeState("selectedNode");

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
                        $scope.error = { errorMsg: "Error Copying Media", data:{Message:result.message}};
                        $scope.success = false;
                        $scope.busy = false;
                    }

                }, function (err) {
                    $scope.success = false;
                    $scope.error = err;
                    $scope.busy = false;
                });
	    };

	    $scope.dialogTreeEventHandler.bind("treeNodeSelect", nodeSelectHandler);
	    $scope.dialogTreeEventHandler.bind("treeNodeExpanded", nodeExpandedHandler);

	    $scope.$on('$destroy', function () {
	        $scope.dialogTreeEventHandler.unbind("treeNodeSelect", nodeSelectHandler);
	        $scope.dialogTreeEventHandler.unbind("treeNodeExpanded", nodeExpandedHandler);
	    });
	});