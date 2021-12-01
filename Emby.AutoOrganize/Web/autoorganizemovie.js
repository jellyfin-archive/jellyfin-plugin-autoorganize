function getMovieFileName(value) {
    const movieName = 'Movie Name';
    const movieYear = '2017';
    const fileNameWithoutExt = movieName + '.' + movieYear + '.MULTI.1080p.BluRay.DTS.x264-UTT';

    const result = value.replace('%mn', movieName)
        .replace('%m.n', movieName.replace(' ', '.'))
        .replace('%m_n', movieName.replace(' ', '_'))
        .replace('%my', movieYear)
        .replace('%ext', 'mkv')
        .replace('%fn', fileNameWithoutExt);

    return result;
}

function getMovieFolderFileName(value) {
    const movieName = 'Movie Name';
    const movieYear = '2017';
    const fileNameWithoutExt = movieName + '.' + movieYear + '.MULTI.1080p.BluRay.DTS.x264-UTT';

    const result = value.replace('%mn', movieName)
        .replace('%m.n', movieName.replace(' ', '.'))
        .replace('%m_n', movieName.replace(' ', '_'))
        .replace('%my', movieYear)
        .replace('%ext', 'mkv')
        .replace('%fn', fileNameWithoutExt);

    return result;
}

function loadPage(view, config) {
    const movieOptions = config.MovieOptions;

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
        const movieOptions = config.MovieOptions;

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
        movieOptions.MovieFolderPattern = view.querySelector('#txtMovieFolderPattern').value;

        const watchLocation = view.querySelector('#txtWatchMovieFolder').value;
        movieOptions.WatchLocations = watchLocation ? [watchLocation] : [];

        movieOptions.CopyOriginalFile = view.querySelector('#copyOrMoveMovieFile').value;

        movieOptions.QueueLibraryScan = view.querySelector('#chkQueueLibScan').checked;

        ApiClient.updateNamedConfiguration('autoorganize', config).then(Dashboard.processServerConfigurationUpdateResult, Dashboard.processErrorResponse);
    });

    return false;
}

function onApiFailure(e) {
    Loading.hide();

    Dashboard.alert({
        title: 'Error',
        text: 'Error: ' + e.headers.get('X-Application-Error-Code')
    });
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
    function updateMoviePatternHelp() {
        let value = view.querySelector('#txtMoviePattern').value;
        value = getMovieFileName(value);

        const replacementHtmlResult = 'Result: ' + value;

        view.querySelector('.moviePatternDescription').innerHTML = replacementHtmlResult;
    }

    function updateMovieFolderPatternHelp() {
        let value = view.querySelector('#txtMovieFolderPattern').value;
        value = getMovieFolderFileName(value);

        const replacementHtmlResult = 'Result: ' + value;

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
        const picker = new Dashboard.DirectoryBrowser();

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
        if (view.querySelector('#txtMoviePattern').value.includes('/')) {
            // TODO Validate
        }
    }

    function populateMovieLocation(config) {
        const movieOptions = config.MovieOptions;

        ApiClient.getVirtualFolders().then(function (result) {
            const mediasLocations = [];

            for (let n = 0; n < result.length; n++) {
                const virtualFolder = result[n];

                for (let i = 0, length = virtualFolder.Locations.length; i < length; i++) {
                    const location = {
                        value: virtualFolder.Locations[i],
                        display: virtualFolder.Name + ': ' + virtualFolder.Locations[i]
                    };

                    if (virtualFolder.CollectionType == 'movies') {
                        mediasLocations.push(location);
                    }
                }
            }

            let mediasFolderHtml = mediasLocations.map(function (s) {
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
        LibraryMenu.setTabs('autoorganize', 2, getTabs);

        ApiClient.getNamedConfiguration('autoorganize').then(function (config) {
            loadPage(view, config);
            updateMoviePatternHelp();
            updateMovieFolderPatternHelp();
            populateMovieLocation(config);
            toggleMovieLocation();
            toggleMovieFolderPattern();
        });
    });
}
