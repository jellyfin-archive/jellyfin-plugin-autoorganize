
ApiClient.getFileOrganizationResults = function (options) {
    const url = this.getUrl('Library/FileOrganizations', options || {});

    return this.getJSON(url);
};

ApiClient.deleteOriginalFileFromOrganizationResult = function (id) {
    const url = this.getUrl('Library/FileOrganizations/' + id + '/File');

    return this.ajax({
        type: 'DELETE',
        url: url
    });
};

ApiClient.clearOrganizationLog = function () {
    const url = this.getUrl('Library/FileOrganizations');

    return this.ajax({
        type: 'DELETE',
        url: url
    });
};

ApiClient.clearOrganizationCompletedLog = function () {
    const url = this.getUrl('Library/FileOrganizations/Completed');

    return this.ajax({
        type: 'DELETE',
        url: url
    });
};

ApiClient.performOrganization = function (id) {
    const url = this.getUrl('Library/FileOrganizations/' + id + '/Organize');

    return this.ajax({
        type: 'POST',
        url: url
    });
};

ApiClient.performEpisodeOrganization = function (id, options) {
    const url = this.getUrl('Library/FileOrganizations/' + id + '/Episode/Organize');

    return this.ajax({
        type: 'POST',
        url: url,
        data: JSON.stringify(options),
        contentType: 'application/json'
    });
};

ApiClient.performMovieOrganization = function (id, options) {
    const url = this.getUrl('Library/FileOrganizations/' + id + '/Movie/Organize');

    return this.ajax({
        type: 'POST',
        url: url,
        data: JSON.stringify(options),
        contentType: 'application/json'
    });
};

ApiClient.getSmartMatchInfos = function (options) {
    options = options || {};

    const url = this.getUrl('Library/FileOrganizations/SmartMatches', options);

    return this.ajax({
        type: 'GET',
        url: url,
        dataType: 'json'
    });
};

ApiClient.deleteSmartMatchEntries = function (entries) {
    const url = this.getUrl('Library/FileOrganizations/SmartMatches/Delete');

    const postData = {
        Entries: entries
    };

    return this.ajax({

        type: 'POST',
        url: url,
        data: JSON.stringify(postData),
        contentType: 'application/json'
    });
};

const query = {

    StartIndex: 0,
    Limit: 50
};

let currentResult;
let pageGlobal;

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
    const item = currentResult.Items.filter(function (i) {
        return i.Id === id;
    })[0];

    Dashboard.alert({

        title: getStatusText(item, false),
        message: item.StatusMessage
    });
}

function deleteOriginalFile(page, id) {
    const item = currentResult.Items.filter(function (i) {
        return i.Id === id;
    })[0];

    const message = 'The following file will be deleted:' + '<br/><br/>' + item.OriginalPath + '<br/><br/>' + 'Are you sure you wish to proceed?';

    Dashboard.confirm(message, 'Delete File').then(function () {
        Loading.show();

        ApiClient.deleteOriginalFileFromOrganizationResult(id).then(function () {
            Loading.hide();

            reloadItems(page, true);
        }, Dashboard.processErrorResponse);
    });
}

function organizeFileWithCorrections(page, item) {
    showCorrectionPopup(page, item);
}

function showCorrectionPopup(page, item) {
    import(Dashboard.getConfigurationResourceUrl('FileOrganizerJs')).then(({default: fileorganizer}) => {
        fileorganizer.show(item).then(function () {
            reloadItems(page, false);
        },
        function () { /* Do nothing on reject */ });
    });
}

function organizeFile(page, id) {
    const item = currentResult.Items.filter(function (i) {
        return i.Id === id;
    })[0];

    if (!item.TargetPath) {
        organizeFileWithCorrections(page, item);

        return;
    }

    let message = 'The following file will be moved from:' + '<br/><br/>' + item.OriginalPath + '<br/><br/>' + 'To:' + '<br/><br/>' + item.TargetPath;

    if (item.DuplicatePaths.length) {
        message += '<br/><br/>' + 'The following duplicates will be deleted:';

        message += '<br/><br/>' + item.DuplicatePaths.join('<br/>');
    }

    message += '<br/><br/>' + 'Are you sure you wish to proceed?';

    Dashboard.confirm(message, 'Organize File').then(function () {
        Loading.show();

        ApiClient.performOrganization(id).then(function () {
            Loading.hide();

            reloadItems(page, true);
        }, Dashboard.processErrorResponse);
    });
}

function reloadItems(page, showSpinner) {
    if (showSpinner) {
        Loading.show();
    }

    ApiClient.getFileOrganizationResults(query).then(function (result) {
        currentResult = result;
        renderResults(page, result);

        Loading.hide();
    }, Dashboard.processErrorResponse);
}

function getStatusText(item, enhance) {
    let status = item.Status;

    let color = null;

    if (status === 'SkippedExisting') {
        color = 'blue';
        status = 'Skipped';
    } else if (status === 'Failure') {
        color = 'red';
        status = 'Failed';
    }
    if (status === 'Success') {
        color = 'green';
        status = 'Success';
    }

    if (enhance) {
        if (item.StatusMessage) {
            return '<a style="color:' + color + ';" data-resultid="' + item.Id + '" is="emby-button" href="#" class="button-link btnShowStatusMessage">' + status + '</a>';
        } else {
            return '<span data-resultid="' + item.Id + '" style="color:' + color + ';">' + status + '</span>';
        }
    }

    return status;
}

function getQueryPagingHtml(options) {
    const startIndex = options.startIndex;
    const limit = options.limit;
    const totalRecordCount = options.totalRecordCount;

    let html = '';

    const recordsEnd = Math.min(startIndex + limit, totalRecordCount);

    const showControls = limit < totalRecordCount;

    html += '<div class="listPaging">';

    if (showControls) {
        html += '<span style="vertical-align:middle;">';

        const startAtDisplay = totalRecordCount ? startIndex + 1 : 0;
        html += startAtDisplay + '-' + recordsEnd + ' of ' + totalRecordCount;

        html += '</span>';

        html += '<div style="display:inline-block;">';

        html += '<button is="paper-icon-button-light" class="btnPreviousPage autoSize" ' + (startIndex ? '' : 'disabled') + '><span class="material-icons arrow_back"></span></button>';
        html += '<button is="paper-icon-button-light" class="btnNextPage autoSize" ' + (startIndex + limit >= totalRecordCount ? 'disabled' : '') + '><span class="material-icons arrow_forward"></span></button>';

        html += '</div>';
    }

    html += '</div>';

    return html;
}

function renderResults(page, result) {
    if (Object.prototype.toString.call(page) !== '[object Window]') {
        const rows = result.Items.map(function (item) {
            let html = '';

            html += '<tr class="detailTableBodyRow detailTableBodyRow-shaded" id="row' + item.Id + '">';

            html += renderItemRow(item);

            html += '</tr>';

            return html;
        }).join('');

        const resultBody = page.querySelector('.resultBody');
        resultBody.innerHTML = rows;

        resultBody.addEventListener('click', handleItemClick);

        const pagingHtml = getQueryPagingHtml({
            startIndex: query.StartIndex,
            limit: query.Limit,
            totalRecordCount: result.TotalRecordCount,
            showLimit: false,
            updatePageSizeSetting: false
        });

        const topPaging = page.querySelector('.listTopPaging');
        topPaging.innerHTML = pagingHtml;

        const bottomPaging = page.querySelector('.listBottomPaging');
        bottomPaging.innerHTML = pagingHtml;

        const btnNextTop = topPaging.querySelector('.btnNextPage');
        const btnNextBottom = bottomPaging.querySelector('.btnNextPage');
        const btnPrevTop = topPaging.querySelector('.btnPreviousPage');
        const btnPrevBottom = bottomPaging.querySelector('.btnPreviousPage');

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

        const btnClearLog = page.querySelector('.btnClearLog');
        const btnClearCompleted = page.querySelector('.btnClearCompleted');

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
    let html = '';

    html += '<td class="detailTableBodyCell">';
    const hide = item.IsInProgress ? '' : ' hide';
    html += '<img src="css/images/throbber.gif" alt="" class="syncSpinner' + hide + '" style="vertical-align: middle;" />';
    html += '</td>';

    html += '<td class="detailTableBodyCell" data-title="Date">';
    const date = Dashboard.datetime.parseISO8601Date(item.Date, true);
    html += Dashboard.datetime.toLocaleDateString(date);
    html += '</td>';

    html += '<td data-title="Source" class="detailTableBodyCell fileCell">';
    const status = item.Status;

    if (item.IsInProgress) {
        html += '<span style="color:darkorange;">';
        html += item.OriginalFileName;
        html += '</span>';
    } else if (status === 'SkippedExisting') {
        html += '<a is="emby-button" data-resultid="' + item.Id + '" style="color:blue;" href="#" class="button-link btnShowStatusMessage">';
        html += item.OriginalFileName;
        html += '</a>';
    } else if (status === 'Failure') {
        html += '<a is="emby-button" data-resultid="' + item.Id + '" style="color:red;" href="#" class="button-link btnShowStatusMessage">';
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
        html += '<button type="button" is="paper-icon-button-light" data-resultid="' + item.Id + '" class="btnProcessResult organizerButton autoSize" title="Organize"><span class="material-icons edit"></span></button>';
        html += '<button type="button" is="paper-icon-button-light" data-resultid="' + item.Id + '" class="btnDeleteResult organizerButton autoSize" title="Delete"><span class="material-icons delete"></span></button>';
    }

    html += '</td>';

    return html;
}

function handleItemClick(e) {
    let id;

    const buttonStatus = parentWithClass(e.target, 'btnShowStatusMessage');
    if (buttonStatus) {
        id = buttonStatus.getAttribute('data-resultid');
        showStatusMessage(id);
    }

    const buttonOrganize = parentWithClass(e.target, 'btnProcessResult');
    if (buttonOrganize) {
        id = buttonOrganize.getAttribute('data-resultid');
        organizeFile(e.view, id);
    }

    const buttonDelete = parentWithClass(e.target, 'btnDeleteResult');
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
    const rowId = '#row' + item.Id;
    const row = page.querySelector(rowId);

    if (row) {
        row.innerHTML = renderItemRow(item);
    }
}

function getTabs() {
    return [
        {
            href: Dashboard.getPluginUrl('AutoOrganizeLog'),
            name: 'Activity Log'
        },
        {
            href: Dashboard.getPluginUrl('AutoOrganizeTv'),
            name: 'TV'
        },
        {
            href: Dashboard.getPluginUrl('AutoOrganizeMovie'),
            name: 'Movie'
        },
        {
            href: Dashboard.getPluginUrl('AutoOrganizeSmart'),
            name: 'Smart Matches'
        }];
}

export default function (view, params) {
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
        LibraryMenu.setTabs('autoorganize', 0, getTabs);

        reloadItems(view, true);

        events.on(ServerNotifications, 'AutoOrganize_LogReset', onServerEvent);
        events.on(ServerNotifications, 'AutoOrganize_ItemUpdated', onServerEvent);
        events.on(ServerNotifications, 'AutoOrganize_ItemRemoved', onServerEvent);
        events.on(ServerNotifications, 'AutoOrganize_ItemAdded', onServerEvent);
        events.on(ServerNotifications, 'ScheduledTaskEnded', onServerEvent);

        // on here
        TaskButton({
            mode: 'on',
            progressElem: view.querySelector('.organizeProgress'),
            panel: view.querySelector('.organizeTaskPanel'),
            taskKey: 'AutoOrganize',
            button: view.querySelector('.btnOrganize')
        });
    });

    view.addEventListener('viewhide', function (e) {
        currentResult = null;

        events.off(ServerNotifications, 'AutoOrganize_LogReset', onServerEvent);
        events.off(ServerNotifications, 'AutoOrganize_ItemUpdated', onServerEvent);
        events.off(ServerNotifications, 'AutoOrganize_ItemRemoved', onServerEvent);
        events.off(ServerNotifications, 'AutoOrganize_ItemAdded', onServerEvent);
        events.off(ServerNotifications, 'ScheduledTaskEnded', onServerEvent);

        // off here
        TaskButton({
            mode: 'off',
            button: view.querySelector('.btnOrganize')
        });
    });
}
