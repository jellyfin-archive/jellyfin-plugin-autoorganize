define(['dialogHelper', 'loading', 'emby-checkbox', 'emby-input', 'emby-button', 'emby-select', 'paper-icon-button-light', 'formDialogStyle'], function (dialogHelper, loading) {
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

    var chosenType;
    var extractedName;
    var extractedYear;
    var currentNewItem;
    var existingMediasHtml;
    var mediasLocationsCount = 0;

    function onApiFailure(e) {

        loading.hide();

        require(['alert'], function (alert) {
            alert({
                title: 'Error',
                text: 'Error: ' + e.headers.get("X-Application-Error-Code")
            });
        });
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

        loading.show();
        ApiClient.getItems(null, {
            recursive: true,
            includeItemTypes: chosenType,
            sortBy: 'SortName'

        }).then(function (result) {

            loading.hide();

            existingMediasHtml = result.Items.map(function (s) {

                return '<option value="' + s.Id + '">' + s.Name + '</option>';

            }).join('');

            existingMediasHtml = '<option value=""></option>' + existingMediasHtml;

            context.querySelector('#selectMedias').innerHTML = existingMediasHtml;

            ApiClient.getVirtualFolders().then(function (result) {

                var mediasLocations = [];

                for (var n = 0; n < result.length; n++) {

                    var virtualFolder = result[n];

                    for (var i = 0, length = virtualFolder.Locations.length; i < length; i++) {
                        var location = {
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

                var mediasFolderHtml = mediasLocations.map(function (s) {
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
        }
        else {
            context.querySelector('.fldRemember').classList.remove('hide');
        }

        context.querySelector('#txtSeason').value = item.ExtractedSeasonNumber;
        context.querySelector('#txtEpisode').value = item.ExtractedEpisodeNumber;
        context.querySelector('#txtEndingEpisode').value = item.ExtractedEndingEpisodeNumber;

        context.querySelector('#chkRememberCorrection').checked = false;

        populateMedias(context);
    }

    function submitMediaForm(dlg) {

        loading.show();

        var resultId = dlg.querySelector('#hfResultId').value;
        var mediaId = dlg.querySelector('#selectMedias').value;

        var targetFolder = null;
        var newProviderIds = null;
        var newMediaName = null;
        var newMediaYear = null;

        if (mediaId == "##NEW##" && currentNewItem != null) {
            mediaId = null;
            newProviderIds = currentNewItem.ProviderIds;
            newMediaName = currentNewItem.Name;
            newMediaYear = currentNewItem.ProductionYear;
            targetFolder = dlg.querySelector('#selectMediaFolder').value;
        }

        if (chosenType == 'Series') {
            var options = {

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

                loading.hide();

                dlg.submitted = true;
                dialogHelper.close(dlg);

            }, onApiFailure);
        } else if (chosenType == 'Movie') {
            var options = {

                MovieId: mediaId,
                NewMovieProviderIds: newProviderIds,
                NewMovieName: newMediaName,
                NewMovieYear: newMediaYear,
                TargetFolder: targetFolder
            };

            ApiClient.performMovieOrganization(resultId, options).then(function () {

                loading.hide();

                dlg.submitted = true;
                dialogHelper.close(dlg);

            }, onApiFailure);
        }


    }

    function showNewMediaDialog(dlg) {

        if (mediasLocationsCount == 0) {

            require(['alert'], function (alert) {
                alert({
                    title: 'Error',
                    text: 'No TV libraries are configured in Emby library setup.'
                });
            });
            return;
        }

        require(['itemIdentifier'], function (itemIdentifier) {

            itemIdentifier.showFindNew(extractedName, extractedYear, chosenType, ApiClient.serverId()).then(function (newItem) {

                if (newItem != null) {
                    currentNewItem = newItem;
                    var mediasHtml = existingMediasHtml;
                    mediasHtml = mediasHtml + '<option selected value="##NEW##">' + currentNewItem.Name + '</option>';
                    dlg.querySelector('#selectMedias').innerHTML = mediasHtml;
                    selectedMediasChanged(dlg);
                }
            });
        });
    }

    function selectedMediasChanged(dlg) {
        var mediasId = dlg.querySelector('#selectMedias').value;

        if (mediasId == "##NEW##") {
            dlg.querySelector('.fldSelectMediaFolder').classList.remove('hide');
            dlg.querySelector('#selectMediaFolder').setAttribute('required', 'required');
        }
        else {
            dlg.querySelector('.fldSelectMediaFolder').classList.add('hide');
            dlg.querySelector('#selectMediaFolder').removeAttribute('required');
        }
    }

    function selectedMediaTypeChanged(dlg, item) {
        var mediaType = dlg.querySelector('#selectMediaType').value;

        switch (mediaType) {
            case "":
                dlg.querySelector('#divPermitChoice').classList.add('hide');
                dlg.querySelector('#divGlobalChoice').classList.add('hide');
                dlg.querySelector('#divEpisodeChoice').classList.add('hide');
                break;
            case "Movie":
                dlg.querySelector('#selectMedias').setAttribute('label', 'Movie');
                dlg.querySelector('[for="selectMedias"]').innerHTML =  'Movie';

                dlg.querySelector('#divPermitChoice').classList.remove('hide');
                dlg.querySelector('#divGlobalChoice').classList.remove('hide');
                dlg.querySelector('#divEpisodeChoice').classList.add('hide');

                dlg.querySelector('#txtSeason').removeAttribute('required');
                dlg.querySelector('#txtEpisode').removeAttribute('required');

                initMovieForm(dlg, item);

                break;
            case "Episode":
                dlg.querySelector('#selectMedias').setAttribute('label', 'Series');
                dlg.querySelector('[for="selectMedias"]').innerHTML = 'Series';

                dlg.querySelector('#divPermitChoice').classList.remove('hide');
                dlg.querySelector('#divGlobalChoice').classList.remove('hide');
                dlg.querySelector('#divEpisodeChoice').classList.remove('hide');

                dlg.querySelector('#txtSeason').setAttribute('required', 'required');
                dlg.querySelector('#txtEpisode').setAttribute('required', 'required');

                initEpisodeForm(dlg, item);
                break;
        }
    }

    return {
        show: function (item) {
            return new Promise(function (resolve, reject) {

                extractedName = null;
                extractedYear = null;
                currentNewItem = null;
                existingMediasHtml = null;

                var xhr = new XMLHttpRequest();
                xhr.open('GET', Dashboard.getConfigurationResourceUrl('FileOrganizerHtml'), true);

                xhr.onload = function (e) {

                    var template = this.response;
                    var dlg = dialogHelper.createDialog({
                        removeOnClose: true,
                        size: 'small'
                    });

                    dlg.classList.add('ui-body-a');
                    dlg.classList.add('background-theme-a');

                    dlg.classList.add('formDialog');

                    var html = '';

                    html += template;

                    dlg.innerHTML = html;

                    dlg.querySelector('.formDialogHeaderTitle').innerHTML = 'Organize';

                    dialogHelper.open(dlg);

                    dlg.addEventListener('close', function () {

                        if (dlg.submitted) {
                            resolve();
                        } else {
                            reject();
                        }
                    });

                    dlg.querySelector('.btnCancel').addEventListener('click', function (e) {

                        dialogHelper.close(dlg);
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
                }

                xhr.send();
            });
        }
    };
});