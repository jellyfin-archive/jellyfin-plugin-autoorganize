define(['globalize', 'serverNotifications', 'events', 'scripts/taskbutton', 'datetime', 'loading', 'libraryMenu', 'libraryBrowser', 'paper-icon-button-light', 'emby-linkbutton', 'detailtablecss'], function (globalize, serverNotifications, events, taskButton, datetime, loading, libraryMenu, libraryBrowser) {
    'use strict';

    ApiClient.getFileOrganizationResults = function (options) {

        var url = this.getUrl("Library/FileOrganization", options || {});

        return this.getJSON(url);
    };

    ApiClient.deleteOriginalFileFromOrganizationResult = function (id) {

        var url = this.getUrl("Library/FileOrganizations/" + id + "/File");

        return this.ajax({
            type: "DELETE",
            url: url
        });
    };

    ApiClient.clearOrganizationLog = function () {

        var url = this.getUrl("Library/FileOrganizations");

        return this.ajax({
            type: "DELETE",
            url: url
        });
    };

    ApiClient.clearOrganizationCompletedLog = function () {

        var url = this.getUrl("Library/FileOrganizations/Completed");

        return this.ajax({
            type: "DELETE",
            url: url
        });
    };

    ApiClient.performOrganization = function (id) {

        var url = this.getUrl("Library/FileOrganizations/" + id + "/Organize");

        return this.ajax({
            type: "POST",
            url: url
        });
    };

    ApiClient.performEpisodeOrganization = function (id, options) {

        var url = this.getUrl("Library/FileOrganizations/" + id + "/Episode/Organize");

        return this.ajax({
            type: "POST",
            url: url,
            data: JSON.stringify(options),
            contentType: 'application/json'
        });
    };

    ApiClient.performMovieOrganization = function (id, options) {

        var url = this.getUrl("Library/FileOrganizations/" + id + "/Movie/Organize");

        return this.ajax({
            type: "POST",
            url: url,
            data: JSON.stringify(options),
            contentType: 'application/json'
        });
    };

    ApiClient.getSmartMatchInfos = function (options) {

        options = options || {};

        var url = this.getUrl("Library/FileOrganizations/SmartMatches", options);

        return this.ajax({
            type: "GET",
            url: url,
            dataType: "json"
        });
    };

    ApiClient.deleteSmartMatchEntries = function (entries) {

        var url = this.getUrl("Library/FileOrganizations/SmartMatches/Delete");

        var postData = {
            Entries: entries
        };

        return this.ajax({

            type: "POST",
            url: url,
            data: JSON.stringify(postData),
            contentType: "application/json"
        });
    };

    var query = {

        StartIndex: 0,
        Limit: 50
    };

    var currentResult;
    var pageGlobal;

    function parentWithClass(elem, className) {

        while (!elem.classList || !elem.classList.contains(className)) {
            elem = elem.parentNode;

            if (!elem) {
                return null;
            }
        }

        return elem;
    }

    function showStatusMessage(id) {

        var item = currentResult.Items.filter(function (i) {

            return i.Id === id;
        })[0];

        Dashboard.alert({

            title: getStatusText(item, false),
            message: item.StatusMessage
        });
    }

    function deleteOriginalFile(page, id) {

        var item = currentResult.Items.filter(function (i) {

            return i.Id === id;
        })[0];

        var message = 'The following file will be deleted:' + '<br/><br/>' + item.OriginalPath + '<br/><br/>' + 'Are you sure you wish to proceed?';

        require(['confirm'], function (confirm) {

            confirm(message, 'Delete File').then(function () {

                loading.show();

                ApiClient.deleteOriginalFileFromOrganizationResult(id).then(function () {

                    loading.hide();

                    reloadItems(page, true);

                }, Dashboard.processErrorResponse);
            });
        });
    }

    function organizeFileWithCorrections(page, item) {

        showCorrectionPopup(page, item);
    }

    function showCorrectionPopup(page, item) {

        require([Dashboard.getConfigurationResourceUrl('FileOrganizerJs')], function (fileorganizer) {

            fileorganizer.show(item).then(function () {
                reloadItems(page, false);
            },
            function () { /* Do nothing on reject */ });
        });
    }

    function organizeFile(page, id) {

        var item = currentResult.Items.filter(function (i) {

            return i.Id === id;
        })[0];

        if (!item.TargetPath) {
            organizeFileWithCorrections(page, item);

            return;
        }

        var message = 'The following file will be moved from:' + '<br/><br/>' + item.OriginalPath + '<br/><br/>' + 'To:' + '<br/><br/>' + item.TargetPath;

        if (item.DuplicatePaths.length) {
            message += '<br/><br/>' + 'The following duplicates will be deleted:';

            message += '<br/><br/>' + item.DuplicatePaths.join('<br/>');
        }

        message += '<br/><br/>' + 'Are you sure you wish to proceed?';

        require(['confirm'], function (confirm) {

            confirm(message, 'Organize File').then(function () {

                loading.show();

                ApiClient.performOrganization(id).then(function () {

                    loading.hide();

                    reloadItems(page, true);

                }, Dashboard.processErrorResponse);
            });
        });
    }

    function reloadItems(page, showSpinner) {

        if (showSpinner) {
            loading.show();
        }

        ApiClient.getFileOrganizationResults(query).then(function (result) {

            currentResult = result;
            renderResults(page, result);

            loading.hide();
        }, Dashboard.processErrorResponse);
    }

    function getStatusText(item, enhance) {

        var status = item.Status;

        var color = null;

        if (status === 'SkippedExisting') {
            status = 'Skipped';
        }
        else if (status === 'Failure') {
            color = '#cc0000';
            status = 'Failed';
        }
        if (status === 'Success') {
            color = 'green';
            status = 'Success';
        }

        if (enhance) {

            if (item.StatusMessage) {

                return '<a style="color:' + color + ';" data-resultid="' + item.Id + '" is="emby-linkbutton" href="#" class="button-link btnShowStatusMessage">' + status + '</a>';
            } else {
                return '<span data-resultid="' + item.Id + '" style="color:' + color + ';">' + status + '</span>';
            }
        }

        return status;
    }

    function renderResults(page, result) {

        if (Object.prototype.toString.call(page) !== "[object Window]") {

            var rows = result.Items.map(function (item) {

                var html = '';

                html += '<tr class="detailTableBodyRow detailTableBodyRow-shaded" id="row' + item.Id + '">';

                html += renderItemRow(item);

                html += '</tr>';

                return html;
            }).join('');

            var resultBody = page.querySelector('.resultBody');
            resultBody.innerHTML = rows;

            resultBody.addEventListener('click', handleItemClick);

            var pagingHtml = libraryBrowser.getQueryPagingHtml({
                startIndex: query.StartIndex,
                limit: query.Limit,
                totalRecordCount: result.TotalRecordCount,
                showLimit: false,
                updatePageSizeSetting: false
            });

            var topPaging = page.querySelector('.listTopPaging');
            topPaging.innerHTML = pagingHtml;

            var bottomPaging = page.querySelector('.listBottomPaging');
            bottomPaging.innerHTML = pagingHtml;

            var btnNextTop = topPaging.querySelector(".btnNextPage");
            var btnNextBottom = bottomPaging.querySelector(".btnNextPage");
            var btnPrevTop = topPaging.querySelector(".btnPreviousPage");
            var btnPrevBottom = bottomPaging.querySelector(".btnPreviousPage");

            if (btnNextTop) {
                btnNextTop.addEventListener('click', function () {
                    query.StartIndex += query.Limit;
                    reloadItems(page, true);
                });
            }

            if (btnNextBottom) {
                btnNextBottom.addEventListener('click', function () {
                    query.StartIndex += query.Limit;
                    reloadItems(page, true);
                });
            }

            if (btnPrevTop) {
                btnPrevTop.addEventListener('click', function () {
                    query.StartIndex -= query.Limit;
                    reloadItems(page, true);
                });
            }

            if (btnPrevBottom) {
                btnPrevBottom.addEventListener('click', function () {
                    query.StartIndex -= query.Limit;
                    reloadItems(page, true);
                });
            }

            var btnClearLog = page.querySelector('.btnClearLog');
            var btnClearCompleted = page.querySelector('.btnClearCompleted');

            if (result.TotalRecordCount) {
                btnClearLog.classList.remove('hide');
                btnClearCompleted.classList.remove('hide');
            } else {
                btnClearLog.classList.add('hide');
                btnClearCompleted.classList.add('hide');
            }
        }
    }

    function renderItemRow(item) {

        var html = '';

        html += '<td class="detailTableBodyCell">';
        var hide = item.IsInProgress ? '' : ' hide';
        html += '<img src="css/images/throbber.gif" alt="" class="syncSpinner' + hide + '" style="vertical-align: middle;" />';
        html += '</td>';

        html += '<td class="detailTableBodyCell" data-title="Date">';
        var date = datetime.parseISO8601Date(item.Date, true);
        html += datetime.toLocaleDateString(date);
        html += '</td>';

        html += '<td data-title="Source" class="detailTableBodyCell fileCell">';
        var status = item.Status;

        if (item.IsInProgress) {
            html += '<span style="color:darkorange;">';
            html += item.OriginalFileName;
            html += '</span>';
        }
        else if (status === 'SkippedExisting') {
            html += '<a is="emby-linkbutton" data-resultid="' + item.Id + '" style="color:blue;" href="#" class="button-link btnShowStatusMessage">';
            html += item.OriginalFileName;
            html += '</a>';
        }
        else if (status === 'Failure') {
            html += '<a is="emby-linkbutton" data-resultid="' + item.Id + '" style="color:red;" href="#" class="button-link btnShowStatusMessage">';
            html += item.OriginalFileName;
            html += '</a>';
        } else {
            html += '<span style="color:green;">';
            html += item.OriginalFileName;
            html += '</span>';
        }
        html += '</td>';

        html += '<td data-title="Destination" class="detailTableBodyCell fileCell">';
        html += item.TargetPath || '';
        html += '</td>';

        html += '<td class="detailTableBodyCell organizerButtonCell" style="whitespace:no-wrap;">';

        if (item.Status !== 'Success') {

            html += '<button type="button" is="paper-icon-button-light" data-resultid="' + item.Id + '" class="btnProcessResult organizerButton autoSize" title="Organize"><i class="md-icon">folder</i></button>';
            html += '<button type="button" is="paper-icon-button-light" data-resultid="' + item.Id + '" class="btnDeleteResult organizerButton autoSize" title="Delete"><i class="md-icon">delete</i></button>';
        }

        html += '</td>';

        return html;
    }

    function handleItemClick(e) {

        var id;

        var buttonStatus = parentWithClass(e.target, 'btnShowStatusMessage');
        if (buttonStatus) {

            id = buttonStatus.getAttribute('data-resultid');
            showStatusMessage(id);
        }

        var buttonOrganize = parentWithClass(e.target, 'btnProcessResult');
        if (buttonOrganize) {

            id = buttonOrganize.getAttribute('data-resultid');
            organizeFile(e.view, id);
        }

        var buttonDelete = parentWithClass(e.target, 'btnDeleteResult');
        if (buttonDelete) {

            id = buttonDelete.getAttribute('data-resultid');
            deleteOriginalFile(e.view, id);
        }
    }

    function onServerEvent(e, apiClient, data) {

        if (e.type === 'ScheduledTaskEnded') {

            if (data && data.Key === 'AutoOrganize') {
                reloadItems(pageGlobal, false);
            }
        } else if (e.type === 'AutoOrganize_ItemUpdated' && data) {

            updateItemStatus(pageGlobal, data);
        } else {

            reloadItems(pageGlobal, false);
        }
    }

    function updateItemStatus(page, item) {

        var rowId = '#row' + item.Id;
        var row = page.querySelector(rowId);

        if (row) {

            row.innerHTML = renderItemRow(item);
        }
    }

    function getTabs() {
        return [
            {
                href: Dashboard.getConfigurationPageUrl('AutoOrganizeLog'),
                name: 'Activity Log'
            },
            {
                href: Dashboard.getConfigurationPageUrl('AutoOrganizeTv'),
                name: 'TV'
            },
            {
                href: Dashboard.getConfigurationPageUrl('AutoOrganizeMovie'),
                name: 'Movie'
            },
            {
                href: Dashboard.getConfigurationPageUrl('AutoOrganizeSmart'),
                name: 'Smart Matches'
            }];
    }

    return function (view, params) {

        pageGlobal = view;

        view.querySelector('.btnClearLog').addEventListener('click', function () {

            ApiClient.clearOrganizationLog().then(function () {
                query.StartIndex = 0;
                reloadItems(view, true);
            }, Dashboard.processErrorResponse);
        });

        view.querySelector('.btnClearCompleted').addEventListener('click', function () {

            ApiClient.clearOrganizationCompletedLog().then(function () {
                query.StartIndex = 0;
                reloadItems(view, true);
            }, Dashboard.processErrorResponse);
        });

        view.addEventListener('viewshow', function (e) {

            libraryMenu.setTabs('autoorganize', 0, getTabs);

            reloadItems(view, true);

            events.on(serverNotifications, 'AutoOrganize_LogReset', onServerEvent);
            events.on(serverNotifications, 'AutoOrganize_ItemUpdated', onServerEvent);
            events.on(serverNotifications, 'AutoOrganize_ItemRemoved', onServerEvent);
            events.on(serverNotifications, 'AutoOrganize_ItemAdded', onServerEvent);
            events.on(serverNotifications, 'ScheduledTaskEnded', onServerEvent);

            // on here
            taskButton({
                mode: 'on',
                progressElem: view.querySelector('.organizeProgress'),
                panel: view.querySelector('.organizeTaskPanel'),
                taskKey: 'AutoOrganize',
                button: view.querySelector('.btnOrganize')
            });
        });

        view.addEventListener('viewhide', function (e) {

            currentResult = null;

            events.off(serverNotifications, 'AutoOrganize_LogReset', onServerEvent);
            events.off(serverNotifications, 'AutoOrganize_ItemUpdated', onServerEvent);
            events.off(serverNotifications, 'AutoOrganize_ItemRemoved', onServerEvent);
            events.off(serverNotifications, 'AutoOrganize_ItemAdded', onServerEvent);
            events.off(serverNotifications, 'ScheduledTaskEnded', onServerEvent);

            // off here
            taskButton({
                mode: 'off',
                button: view.querySelector('.btnOrganize')
            });
        });
    };
});