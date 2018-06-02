define(['libraryMenu', 'emby-input', 'emby-select', 'emby-checkbox', 'emby-button', 'emby-collapse'], function (libraryMenu) {
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

    function getMovieFileName(value) {
        var movieName = "Movie Name";
        var movieYear = "2017";
        var fileNameWithoutExt = movieName + '.' + movieYear + '.MULTI.1080p.BluRay.DTS.x264-UTT';

        var result = value.replace('%mn', movieName)
            .replace('%m.n', movieName.replace(' ', '.'))
            .replace('%m_n', movieName.replace(' ', '_'))
            .replace('%my', movieYear)
            .replace('%ext', 'mkv')
            .replace('%fn', fileNameWithoutExt);

        return result;
    }

    function getMovieFolderFileName(value) {
        var movieName = "Movie Name";
        var movieYear = "2017";
        var fileNameWithoutExt = movieName + '.' + movieYear + '.MULTI.1080p.BluRay.DTS.x264-UTT';

        var result = value.replace('%mn', movieName)
            .replace('%m.n', movieName.replace(' ', '.'))
            .replace('%m_n', movieName.replace(' ', '_'))
            .replace('%my', movieYear)
            .replace('%ext', 'mkv')
            .replace('%fn', fileNameWithoutExt);

        return result;
    }

    function loadPage(view, config) {

        var movieOptions = config.MovieOptions;

        view.querySelector('#chkEnableMovieSorting').checked = movieOptions.IsEnabled;
        view.querySelector('#chkOverwriteExistingMovies').checked = movieOptions.OverwriteExistingFiles;
        view.querySelector('#chkDeleteEmptyMovieFolders').checked = movieOptions.DeleteEmptyFolders;

        view.querySelector('#txtMovieMinFileSize').value = movieOptions.MinFileSizeMb;
        view.querySelector('#txtMoviePattern').value = movieOptions.MoviePattern;
        view.querySelector('#txtWatchMovieFolder').value = movieOptions.WatchLocations[0] || '';

        view.querySelector('#chkSubMovieFolders').checked = movieOptions.MovieFolder;
        view.querySelector('#txtMovieFolderPattern').value = movieOptions.MovieFolderPattern;

        view.querySelector('#txtDeleteLeftOverMovieFiles').value = movieOptions.LeftOverFileExtensionsToDelete.join(';');

        view.querySelector('#chkExtendedClean').checked = movieOptions.ExtendedClean;

        view.querySelector('#chkEnableMovieAutoDetect').checked = movieOptions.AutoDetectMovie;

        view.querySelector('#copyOrMoveMovieFile').value = movieOptions.CopyOriginalFile.toString();

        view.querySelector('#chkQueueLibScan').checked = movieOptions.QueueLibraryScan;
    }

    function onSubmit(view) {

        ApiClient.getNamedConfiguration('autoorganize').then(function (config) {

            var movieOptions = config.MovieOptions;

            movieOptions.IsEnabled = view.querySelector('#chkEnableMovieSorting').checked;
            movieOptions.OverwriteExistingEpisodes = view.querySelector('#chkOverwriteExistingMovies').checked;
            movieOptions.DeleteEmptyFolders = view.querySelector('#chkDeleteEmptyMovieFolders').checked;

            movieOptions.MinFileSizeMb = view.querySelector('#txtMovieMinFileSize').value;
            movieOptions.MoviePattern = view.querySelector('#txtMoviePattern').value;
            movieOptions.LeftOverFileExtensionsToDelete = view.querySelector('#txtDeleteLeftOverMovieFiles').value.split(';');

            movieOptions.ExtendedClean = view.querySelector('#chkExtendedClean').checked;

            movieOptions.AutoDetectMovie = view.querySelector('#chkEnableMovieAutoDetect').checked;
            movieOptions.DefaultMovieLibraryPath = view.querySelector('#selectMovieFolder').value;

            movieOptions.MovieFolder = view.querySelector('#chkSubMovieFolders').checked;
            movieOptions.MovieFolderPattern =  view.querySelector('#txtMovieFolderPattern').value;

            var watchLocation = view.querySelector('#txtWatchMovieFolder').value;
            movieOptions.WatchLocations = watchLocation ? [watchLocation] : [];

            movieOptions.CopyOriginalFile = view.querySelector('#copyOrMoveMovieFile').value;

            movieOptions.QueueLibraryScan = view.querySelector('#chkQueueLibScan').checked;

            ApiClient.updateNamedConfiguration('autoorganize', config).then(Dashboard.processServerConfigurationUpdateResult, Dashboard.processErrorResponse);
        });

        return false;
    }



    function onApiFailure(e) {

        loading.hide();

        require(['alert'], function (alert) {
            alert({
                title: 'Error',
                text: 'Error: ' + e.headers.get("X-Application-Error-Code")
            });
        });
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

        function updateMoviePatternHelp() {

            var value = view.querySelector('#txtMoviePattern').value;
            value = getMovieFileName(value);

            var replacementHtmlResult = 'Result: ' + value;

            view.querySelector('.moviePatternDescription').innerHTML = replacementHtmlResult;
        }

        function updateMovieFolderPatternHelp() {

            var value = view.querySelector('#txtMovieFolderPattern').value;
            value = getMovieFolderFileName(value);

            var replacementHtmlResult = 'Result: ' + value;

            view.querySelector('.movieFolderPatternDescription').innerHTML = replacementHtmlResult;
        }

        function toggleMovieFolderPattern() {
            if (view.querySelector('#chkSubMovieFolders').checked) {
                view.querySelector('.fldSelectMovieFolderPattern').classList.remove('hide');
            } else {
                view.querySelector('.fldSelectMovieFolderPattern').classList.add('hide');
            }
        }

        function selectWatchFolder(e) {

            require(['directorybrowser'], function (directoryBrowser) {

                var picker = new directoryBrowser();

                picker.show({

                    callback: function (path) {

                        if (path) {

                            view.querySelector('#txtWatchMovieFolder').value = path;
                        }
                        picker.close();
                    },
                    header: 'Select Watch Folder',
                    validateWriteable: true
                });
            });
        }

        function toggleMovieLocation() {
            if (view.querySelector('#chkEnableMovieAutoDetect').checked) {
                view.querySelector('.fldSelectMovieFolder').classList.remove('hide');
                view.querySelector('#selectMovieFolder').setAttribute('required', 'required');
            } else {
                view.querySelector('.fldSelectMovieFolder').classList.add('hide');
                view.querySelector('#selectMovieFolder').removeAttribute('required');
            }
        }

        function validate() {
            if (view.querySelector("#txtMoviePattern").value.includes("/")) {
                // TODO Validate
            }
        }

        function populateMovieLocation(config) {

            var movieOptions = config.MovieOptions;

            ApiClient.getVirtualFolders().then(function (result) {

                var mediasLocations = [];

                for (var n = 0; n < result.length; n++) {

                    var virtualFolder = result[n];

                    for (var i = 0, length = virtualFolder.Locations.length; i < length; i++) {
                        var location = {
                            value: virtualFolder.Locations[i],
                            display: virtualFolder.Name + ': ' + virtualFolder.Locations[i]
                        };

                        if (virtualFolder.CollectionType == 'movies') {
                            mediasLocations.push(location);
                        }
                    }
                }

                var mediasFolderHtml = mediasLocations.map(function (s) {
                    return '<option value="' + s.value + '">' + s.display + '</option>';
                }).join('');

                if (mediasLocations.length > 1) {
                    // If the user has multiple folders, add an empty item to enforce a manual selection
                    mediasFolderHtml = '<option value=""></option>' + mediasFolderHtml;
                }

                view.querySelector('#selectMovieFolder').innerHTML = mediasFolderHtml;

                view.querySelector('#selectMovieFolder').value = movieOptions.DefaultMovieLibraryPath;

            }, onApiFailure);
        }

        view.querySelector('#btnSelectWatchMovieFolder').addEventListener('click', selectWatchFolder);

        view.querySelector('#txtMoviePattern').addEventListener('change', updateMoviePatternHelp);
        view.querySelector('#txtMoviePattern').addEventListener('keyup', updateMoviePatternHelp);

        view.querySelector('#chkSubMovieFolders').addEventListener('click', toggleMovieFolderPattern);
        view.querySelector('#txtMovieFolderPattern').addEventListener('change', updateMovieFolderPatternHelp);
        view.querySelector('#txtMovieFolderPattern').addEventListener('keyup', updateMovieFolderPatternHelp);

        view.querySelector('#chkEnableMovieAutoDetect').addEventListener('change', toggleMovieLocation);

        view.querySelector('.libraryFileOrganizerForm').addEventListener('submit', function (e) {
            e.preventDefault();
            validate();
            onSubmit(view);
            return false;
        });

        view.addEventListener('viewshow', function (e) {

            libraryMenu.setTabs('autoorganize', 2, getTabs);

            ApiClient.getNamedConfiguration('autoorganize').then(function (config) {
                loadPage(view, config);
                updateMoviePatternHelp();
                updateMovieFolderPatternHelp();
                populateMovieLocation(config);
                toggleMovieLocation();
                toggleMovieFolderPattern();
            });
        });
    };
});