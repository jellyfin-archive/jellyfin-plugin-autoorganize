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

let chosenType;
let extractedName;
let extractedYear;
let currentNewItem;
let existingMediasHtml;
let mediasLocationsCount = 0;

function onApiFailure(e) {
    Loading.hide();

    // Get message from server or display a default message
    const message =
                e.headers.get('X-Application-Error-Code') ||
                'Server returned status code ' + e.status + ' (' + e.statusText + ') but did not provide a more specific error message.';

    // Show the error using an alert dialog
    Dashboard.alert({ title: 'Error', text: 'Error: ' + message });
}

function initBaseForm(context, item) {
    context.querySelector('.inputFile').innerHTML = item.OriginalFileName;

    context.querySelector('#hfResultId').value = item.Id;

    extractedName = item.ExtractedName;
    extractedYear = item.ExtractedYear;
}

function initMovieForm(context, item) {
    initBaseForm(context, item);

    chosenType = 'Movie';

    populateMedias(context);
}

function populateMedias(context) {
    Loading.show();
    ApiClient.getItems(ApiClient.getCurrentUserId(), {
        recursive: true,
        includeItemTypes: chosenType,
        sortBy: 'SortName'

    }).then(function (result) {
        Loading.hide();

        existingMediasHtml = result.Items.map(function (s) {
            return '<option value="' + s.Id + '">' + s.Name + '</option>';
        }).join('');

        existingMediasHtml = '<option value=""></option>' + existingMediasHtml;

        context.querySelector('#selectMedias').innerHTML = existingMediasHtml;

        ApiClient.getVirtualFolders().then(function (result) {
            const mediasLocations = [];

            for (let n = 0; n < result.length; n++) {
                const virtualFolder = result[n];

                for (let i = 0, length = virtualFolder.Locations.length; i < length; i++) {
                    const location = {
                        value: virtualFolder.Locations[i],
                        display: virtualFolder.Name + ': ' + virtualFolder.Locations[i]
                    };

                    if ((chosenType == 'Movie' && virtualFolder.CollectionType == 'movies') ||
                            (chosenType == 'Series' && virtualFolder.CollectionType == 'tvshows')) {
                        mediasLocations.push(location);
                    }
                }
            }

            mediasLocationsCount = mediasLocations.length;

            let mediasFolderHtml = mediasLocations.map(function (s) {
                return '<option value="' + s.value + '">' + s.display + '</option>';
            }).join('');

            if (mediasLocations.length > 1) {
                // If the user has multiple folders, add an empty item to enforce a manual selection
                mediasFolderHtml = '<option value=""></option>' + mediasFolderHtml;
            }

            context.querySelector('#selectMediaFolder').innerHTML = mediasFolderHtml;
        }, onApiFailure);
    }, onApiFailure);
}

function initEpisodeForm(context, item) {
    initBaseForm(context, item);

    chosenType = 'Series';

    if (!item.ExtractedName || item.ExtractedName.length < 3) {
        context.querySelector('.fldRemember').classList.add('hide');
    } else {
        context.querySelector('.fldRemember').classList.remove('hide');
    }

    context.querySelector('#txtSeason').value = item.ExtractedSeasonNumber;
    context.querySelector('#txtEpisode').value = item.ExtractedEpisodeNumber;
    context.querySelector('#txtEndingEpisode').value = item.ExtractedEndingEpisodeNumber;

    context.querySelector('#chkRememberCorrection').checked = false;

    populateMedias(context);
}

function submitMediaForm(dlg) {
    Loading.show();

    const resultId = dlg.querySelector('#hfResultId').value;
    let mediaId = dlg.querySelector('#selectMedias').value;

    let targetFolder = null;
    let newProviderIds = null;
    let newMediaName = null;
    let newMediaYear = null;

    if (mediaId == '##NEW##' && currentNewItem != null) {
        mediaId = null;
        newProviderIds = currentNewItem.ProviderIds;
        newMediaName = currentNewItem.Name;
        newMediaYear = currentNewItem.ProductionYear;
        targetFolder = dlg.querySelector('#selectMediaFolder').value;
    }

    if (chosenType == 'Series') {
        const options = {

            SeriesId: mediaId,
            SeasonNumber: dlg.querySelector('#txtSeason').value,
            EpisodeNumber: dlg.querySelector('#txtEpisode').value,
            EndingEpisodeNumber: dlg.querySelector('#txtEndingEpisode').value,
            RememberCorrection: dlg.querySelector('#chkRememberCorrection').checked,
            NewSeriesProviderIds: newProviderIds,
            NewSeriesName: newMediaName,
            NewSeriesYear: newMediaYear,
            TargetFolder: targetFolder
        };

        ApiClient.performEpisodeOrganization(resultId, options).then(function () {
            Loading.hide();

            dlg.submitted = true;
            Dashboard.dialogHelper.close(dlg);
        }, onApiFailure);
    } else if (chosenType == 'Movie') {
        const options = {

            MovieId: mediaId,
            NewMovieProviderIds: newProviderIds,
            NewMovieName: newMediaName,
            NewMovieYear: newMediaYear,
            TargetFolder: targetFolder
        };

        ApiClient.performMovieOrganization(resultId, options).then(function () {
            Loading.hide();

            dlg.submitted = true;
            Dashboard.dialogHelper.close(dlg);
        }, onApiFailure);
    }
}

function showNewMediaDialog(dlg) {
    if (mediasLocationsCount == 0) {
        Dashboard.alert({
            title: 'Error',
            text: 'No TV libraries are configured in Jellyfin library setup.'
        });
        return;
    }

    Dashboard.itemIdentifier.showFindNew(extractedName, extractedYear, chosenType, ApiClient.serverId()).then(function (newItem) {
        if (newItem != null) {
            currentNewItem = newItem;
            let mediasHtml = existingMediasHtml;
            mediasHtml = mediasHtml + '<option selected value="##NEW##">' + currentNewItem.Name + '</option>';
            dlg.querySelector('#selectMedias').innerHTML = mediasHtml;
            selectedMediasChanged(dlg);
        }
    });
}

function selectedMediasChanged(dlg) {
    const mediasId = dlg.querySelector('#selectMedias').value;

    if (mediasId == '##NEW##') {
        dlg.querySelector('.fldSelectMediaFolder').classList.remove('hide');
        dlg.querySelector('#selectMediaFolder').setAttribute('required', 'required');
    } else {
        dlg.querySelector('.fldSelectMediaFolder').classList.add('hide');
        dlg.querySelector('#selectMediaFolder').removeAttribute('required');
    }
}

function selectedMediaTypeChanged(dlg, item) {
    const mediaType = dlg.querySelector('#selectMediaType').value;
    const mediaSelector = dlg.querySelector('#selectMedias');

    switch (mediaType) {
        case '':
            dlg.querySelector('#divPermitChoice').classList.add('hide');
            dlg.querySelector('#divGlobalChoice').classList.add('hide');
            dlg.querySelector('#divEpisodeChoice').classList.add('hide');
            break;
        case 'Movie':
            mediaSelector.setAttribute('label', 'Movie');
            if (mediaSelector && mediaSelector.setLabel) mediaSelector.setLabel('Movie');

            dlg.querySelector('#divPermitChoice').classList.remove('hide');
            dlg.querySelector('#divGlobalChoice').classList.remove('hide');
            dlg.querySelector('#divEpisodeChoice').classList.add('hide');

            dlg.querySelector('#txtSeason').removeAttribute('required');
            dlg.querySelector('#txtEpisode').removeAttribute('required');

            initMovieForm(dlg, item);

            break;
        case 'Episode':
            mediaSelector.setAttribute('label', 'Series');
            if (mediaSelector && mediaSelector.setLabel) mediaSelector.setLabel('Series');

            dlg.querySelector('#divPermitChoice').classList.remove('hide');
            dlg.querySelector('#divGlobalChoice').classList.remove('hide');
            dlg.querySelector('#divEpisodeChoice').classList.remove('hide');

            dlg.querySelector('#txtSeason').setAttribute('required', 'required');
            dlg.querySelector('#txtEpisode').setAttribute('required', 'required');

            initEpisodeForm(dlg, item);
            break;
    }
}

export default {
    show: async function (item) {
        extractedName = null;
        extractedYear = null;
        currentNewItem = null;
        existingMediasHtml = null;

        const response = await fetch(Dashboard.getConfigurationResourceUrl('FileOrganizerHtml'));
        const template = await response.text();
        const dlg = Dashboard.dialogHelper.createDialog({
            removeOnClose: true,
            size: 'small'
        });

        dlg.classList.add('ui-body-a');
        dlg.classList.add('background-theme-a');

        dlg.classList.add('formDialog');

        let html = '';

        html += template;

        dlg.innerHTML = html;

        dlg.querySelector('.formDialogHeaderTitle').innerHTML = 'Organize';

        // Add event listeners to dialog
        dlg.addEventListener('close', function () {
            if (dlg.submitted) {
                return;
            } else {
                return;
            }
        });

        dlg.querySelector('.btnCancel').addEventListener('click', function (e) {
            Dashboard.dialogHelper.close(dlg);
        });

        dlg.querySelector('form').addEventListener('submit', function (e) {
            submitMediaForm(dlg);

            e.preventDefault();
            return false;
        });

        dlg.querySelector('#btnNewMedia').addEventListener('click', function (e) {
            showNewMediaDialog(dlg);
        });

        dlg.querySelector('#selectMedias').addEventListener('change', function (e) {
            selectedMediasChanged(dlg);
        });

        dlg.querySelector('#selectMediaType').addEventListener('change', function (e) {
            selectedMediaTypeChanged(dlg, item);
        });

        dlg.querySelector('#selectMediaType').value = item.Type;

        // Init media type
        selectedMediaTypeChanged(dlg, item);

        // Show dialog
        Dashboard.dialogHelper.open(dlg);
    }
};
