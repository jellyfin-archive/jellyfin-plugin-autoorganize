using MediaBrowser.Controller.Configuration;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Logging;
using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Emby.AutoOrganize.Model;
using MediaBrowser.Controller.Dto;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Model.IO;
using Emby.Naming.Common;
using Emby.Naming.Video;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Model.Providers;

namespace Emby.AutoOrganize.Core
{
    public class MovieFileOrganizer
    {
        private readonly ILibraryMonitor _libraryMonitor;
        private readonly ILibraryManager _libraryManager;
        private readonly ILogger _logger;
        private readonly IFileSystem _fileSystem;
        private readonly IFileOrganizationService _organizationService;
        private readonly IServerConfigurationManager _config;
        private readonly IProviderManager _providerManager;

        private readonly CultureInfo _usCulture = new CultureInfo("en-US");

        public MovieFileOrganizer(IFileOrganizationService organizationService, IServerConfigurationManager config, IFileSystem fileSystem, ILogger logger, ILibraryManager libraryManager, ILibraryMonitor libraryMonitor, IProviderManager providerManager)
        {
            _organizationService = organizationService;
            _config = config;
            _fileSystem = fileSystem;
            _logger = logger;
            _libraryManager = libraryManager;
            _libraryMonitor = libraryMonitor;
            _providerManager = providerManager;
        }

        private NamingOptions _namingOptions;
        private NamingOptions GetNamingOptionsInternal()
        {
            if (_namingOptions == null)
            {
                var options = new ExtendedNamingOptions();

                InitNamingOptions(options);

                _namingOptions = options;
            }

            return _namingOptions;
        }

        private void InitNamingOptions(NamingOptions options)
        {
            // These cause apps to have problems
            options.VideoFileExtensions.Remove(".rar");
            options.VideoFileExtensions.Remove(".zip");
            options.VideoFileExtensions.Add(".tp");
        }

        public async Task<FileOrganizationResult> OrganizeMovieFile(string path, AutoOrganizeOptions options, bool overwriteExisting, CancellationToken cancellationToken)
        {
            _logger.Info("Sorting file {0}", path);

            var result = new FileOrganizationResult
            {
                Date = DateTime.UtcNow,
                OriginalPath = path,
                OriginalFileName = Path.GetFileName(path),
                Type = FileOrganizerType.Movie,
                FileSize = _fileSystem.GetFileInfo(path).Length
            };

            try
            {
                if (_libraryMonitor.IsPathLocked(path))
                {
                    result.Status = FileSortingStatus.Failure;
                    result.StatusMessage = "Path is locked by other processes. Please try again later.";
                    _logger.Info("Auto-organize Path is locked by other processes. Please try again later.");
                    return result;
                }

                var namingOptions = GetNamingOptionsInternal();
                var resolver = new VideoResolver(namingOptions);

                var movieInfo = resolver.Resolve(path, false) ??
                    new VideoFileInfo();

                var movieName = movieInfo.Name;

                if (!string.IsNullOrEmpty(movieName))
                {
                    var movieYear = movieInfo.Year;

                    _logger.Debug("Extracted information from {0}. Movie {1}, Year {2}", path, movieName, movieYear);

                    await OrganizeMovie(path,
                        movieName,
                        movieYear,
                        options,
                        overwriteExisting,
                        result,
                        cancellationToken).ConfigureAwait(false);
                }
                else
                {
                    var msg = string.Format("Unable to determine movie name from {0}", path);
                    result.Status = FileSortingStatus.Failure;
                    result.StatusMessage = msg;
                    _logger.Warn(msg);
                }

                var previousResult = _organizationService.GetResultBySourcePath(path);

                if (previousResult != null)
                {
                    // Don't keep saving the same result over and over if nothing has changed
                    if (previousResult.Status == result.Status && previousResult.StatusMessage == result.StatusMessage && result.Status != FileSortingStatus.Success)
                    {
                        return previousResult;
                    }
                }

                await _organizationService.SaveResult(result, CancellationToken.None).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                result.Status = FileSortingStatus.Failure;
                result.StatusMessage = ex.Message;
                _logger.ErrorException("Error organizing file", ex);
            }

            return result;
        }

        private async Task<Movie> CreateNewMovie(MovieFileOrganizationRequest request, string originalPath, AutoOrganizeOptions options, CancellationToken cancellationToken)
        {
            int? newMovieYear = null;
            int year;
            if (int.TryParse(request.NewMovieYear, out year))
            {
                newMovieYear = year;

            }

            // To avoid Movie duplicate by mistake (Missing SmartMatch and wrong selection in UI)
            var movie = GetMatchingMovie(request.NewMovieName, newMovieYear, null, options);

            if (movie == null)
            {
                // We're having a new movie here
                movie = new Movie();
                movie.Id = Guid.NewGuid();
                movie.Name = request.NewMovieName;
                movie.ProductionYear = newMovieYear;

                var newPath =
                     await GetNewPath(originalPath, movie, options.MovieOptions, cancellationToken)
                        .ConfigureAwait(false);

                if (string.IsNullOrEmpty(newPath))
                {
                    var msg = string.Format("Unable to sort {0} because target path could not be determined.", originalPath);
                    throw new Exception(msg);
                }

                movie.Path = Path.Combine(request.TargetFolder, newPath);

                // If no special folder for movie
                movie.IsInMixedFolder = true;

                movie.ProviderIds = request.NewMovieProviderIds;

                // Correctly set the parent of the Movie
                if (_libraryManager.FindByPath(request.TargetFolder, true) is Folder baseFolder)
                    movie.SetParent(baseFolder);

                _libraryManager.CreateItem(movie, cancellationToken);

                var refreshOptions = new MetadataRefreshOptions(_fileSystem);
                await movie.RefreshMetadata(refreshOptions, cancellationToken).ConfigureAwait(false);
            }

            return movie;
        }

        public async Task<FileOrganizationResult> OrganizeWithCorrection(MovieFileOrganizationRequest request, AutoOrganizeOptions options, CancellationToken cancellationToken)
        {
            var result = _organizationService.GetResult(request.ResultId);

            try
            {
                Movie movie = null;

                if (request.NewMovieProviderIds.Count > 0)
                {
                    // To avoid Series duplicate by mistake (Missing SmartMatch and wrong selection in UI)
                    movie = await CreateNewMovie(request, result.OriginalPath, options, cancellationToken).ConfigureAwait(false);
                }

                if (movie == null)
                {
                    // Existing movie
                    movie = (Movie)_libraryManager.GetItemById(new Guid(request.MovieId));
                }

                await OrganizeMovie(result.OriginalPath,
                    movie,
                    options,
                    true,
                    result,
                    cancellationToken).ConfigureAwait(false);

                await _organizationService.SaveResult(result, CancellationToken.None).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                result.Status = FileSortingStatus.Failure;
                result.StatusMessage = ex.Message;
            }

            return result;
        }

        private Task OrganizeMovie(string sourcePath,
            string movieName,
            int? movieYear,
            AutoOrganizeOptions options,
            bool overwriteExisting,
            FileOrganizationResult result,
            CancellationToken cancellationToken)
        {
            var movie = GetMatchingMovie(movieName, movieYear, result, options);

            if (movie == null)
            {
                movie = AutoDetectMovie(movieName, movieYear, result, options, cancellationToken).Result;

                if (movie == null)
                {
                    var msg = string.Format("Unable to find movie in library matching name {0}", movieName);
                    result.Status = FileSortingStatus.Failure;
                    result.StatusMessage = msg;
                    _logger.Warn(msg);
                    return Task.FromResult(true);
                }
            }

            return OrganizeMovie(sourcePath,
                movie,
                options,
                overwriteExisting,
                result,
                cancellationToken);
        }

        private async Task OrganizeMovie(string sourcePath,
            Movie movie,
            AutoOrganizeOptions options,
            bool overwriteExisting,
            FileOrganizationResult result,
            CancellationToken cancellationToken)
        {
            _logger.Info("Sorting file {0} into movie {1}", sourcePath, movie.Path);

            bool isNew = string.IsNullOrWhiteSpace(result.Id);

            if (isNew)
            {
                await _organizationService.SaveResult(result, cancellationToken);
            }

            if (!_organizationService.AddToInProgressList(result, isNew))
            {
                throw new Exception("File is currently processed otherwise. Please try again later.");
            }

            try
            {
                // Proceed to sort the file
                var newPath = movie.Path;
                _logger.Info("Sorting file {0} to new path {1}", sourcePath, newPath);
                result.TargetPath = newPath;

                var fileExists = _fileSystem.FileExists(result.TargetPath);

                if (!overwriteExisting)
                {
                    if (options.MovieOptions.CopyOriginalFile && fileExists && IsSameEpisode(sourcePath, newPath))
                    {
                        var msg = string.Format("File '{0}' already copied to new path '{1}', stopping organization", sourcePath, newPath);
                        _logger.Info(msg);
                        result.Status = FileSortingStatus.SkippedExisting;
                        result.StatusMessage = msg;
                        return;
                    }

                    if (fileExists)
                    {
                        var msg = string.Format("File '{0}' already exists as '{1}', stopping organization", sourcePath, newPath);
                        _logger.Info(msg);
                        result.Status = FileSortingStatus.SkippedExisting;
                        result.StatusMessage = msg;
                        result.TargetPath = newPath;
                        return;
                    }
                }

                PerformFileSorting(options.MovieOptions, result);
            }
            catch (Exception ex)
            {
                result.Status = FileSortingStatus.Failure;
                result.StatusMessage = ex.Message;
                _logger.Warn(ex.Message);
                return;
            }
            finally
            {
                _organizationService.RemoveFromInprogressList(result);
            }
        }

        private void PerformFileSorting(MovieFileOrganizationOptions options, FileOrganizationResult result)
        {
            // We should probably handle this earlier so that we never even make it this far
            if (string.Equals(result.OriginalPath, result.TargetPath, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            _libraryMonitor.ReportFileSystemChangeBeginning(result.TargetPath);

            _fileSystem.CreateDirectory(_fileSystem.GetDirectoryName(result.TargetPath));

            var targetAlreadyExists = _fileSystem.FileExists(result.TargetPath);

            try
            {
                if (targetAlreadyExists || options.CopyOriginalFile)
                {
                    _fileSystem.CopyFile(result.OriginalPath, result.TargetPath, true);
                }
                else
                {
                    _fileSystem.MoveFile(result.OriginalPath, result.TargetPath);
                }

                result.Status = FileSortingStatus.Success;
                result.StatusMessage = string.Empty;
            }
            catch (Exception ex)
            {
                var errorMsg = string.Format("Failed to move file from {0} to {1}: {2}", result.OriginalPath, result.TargetPath, ex.Message);

                result.Status = FileSortingStatus.Failure;
                result.StatusMessage = errorMsg;
                _logger.ErrorException(errorMsg, ex);

                return;
            }
            finally
            {
                _libraryMonitor.ReportFileSystemChangeComplete(result.TargetPath, true);
            }

            if (targetAlreadyExists && !options.CopyOriginalFile)
            {
                try
                {
                    _fileSystem.DeleteFile(result.OriginalPath);
                }
                catch (Exception ex)
                {
                    _logger.ErrorException("Error deleting {0}", ex, result.OriginalPath);
                }
            }
        }

        private async Task<Movie> AutoDetectMovie(string movieName, int? movieYear, FileOrganizationResult result, AutoOrganizeOptions options, CancellationToken cancellationToken)
        {
            if (options.MovieOptions.AutoDetectMovie)
            {
                var parsedName = _libraryManager.ParseName(movieName);

                var yearInName = parsedName.Year;
                var nameWithoutYear = parsedName.Name;

                if (string.IsNullOrWhiteSpace(nameWithoutYear))
                {
                    nameWithoutYear = movieName;
                }

                if (!yearInName.HasValue)
                {
                    yearInName = movieYear;
                }

                #region Search One

                var movieInfo = new MovieInfo
                {
                    Name = nameWithoutYear,
                    Year = yearInName
                };

                var searchResultsTask = _providerManager.GetRemoteSearchResults<Movie, MovieInfo>(new RemoteSearchQuery<MovieInfo>
                {
                    SearchInfo = movieInfo

                }, CancellationToken.None);

                #endregion

                #region Search Two

                // Remote search Hack, some provider does not handle correctly dot as name separator
                var secondSearchName = nameWithoutYear.Replace('.', '_');

                var movieInfo2 = new MovieInfo
                {
                    Name = secondSearchName,
                    Year = yearInName
                };

                var searchResultsTask2 = _providerManager.GetRemoteSearchResults<Movie, MovieInfo>(new RemoteSearchQuery<MovieInfo>
                {
                    SearchInfo = movieInfo2

                }, CancellationToken.None);

                #endregion

                Task.WaitAll(searchResultsTask, searchResultsTask2);

                var listResultOne = searchResultsTask.Result.ToList();
                var listResultTwo = searchResultsTask2.Result.ToList();

                RemoteSearchResult finalResult = null;

                // We need at least one result for the 2 results
                // We permit max 1 result for the autodetection to work
                if (listResultOne.Count <= 1 && listResultTwo.Count <= 1)
                {
                    // if we have only one result for the total, 
                    var resultOne = listResultOne.SingleOrDefault();
                    var resultTwo = listResultTwo.SingleOrDefault();


                    if (resultOne != null && resultTwo != null)
                    {
                        // 2 results, we check if it's the same provider id (at least one)
                        foreach (var resultOneProviders in resultOne.ProviderIds)
                        {
                            if (resultTwo.ProviderIds.TryGetValue(resultOneProviders.Key, out var resultTwoValue) && resultOneProviders.Value == resultTwoValue)
                            {
                                // We got a winner, take the first (unaltered search)
                                finalResult = resultOne;
                                break;
                            }
                        }
                    }
                    else if (resultOne != null)
                    {
                        finalResult = resultOne;
                    }
                    else if (resultTwo != null)
                    {
                        finalResult = resultTwo;
                    }
                }

                if (finalResult != null)
                {
                    // We are in the good position, we can create the item
                    var organizationRequest = new MovieFileOrganizationRequest
                    {
                        NewMovieName = finalResult.Name,
                        NewMovieProviderIds = finalResult.ProviderIds,
                        NewMovieYear = finalResult.ProductionYear.ToString(),
                        TargetFolder = options.MovieOptions.DefaultMovieLibraryPath
                    };

                    return await CreateNewMovie(organizationRequest, result.OriginalPath, options, cancellationToken).ConfigureAwait(false);
                }
            }

            return null;
        }

        private Movie GetMatchingMovie(string movieName, int? movieYear, FileOrganizationResult result, AutoOrganizeOptions options)
        {
            var parsedName = _libraryManager.ParseName(movieName);

            var yearInName = parsedName.Year;
            var nameWithoutYear = parsedName.Name;

            if (string.IsNullOrWhiteSpace(nameWithoutYear))
            {
                nameWithoutYear = movieName;
            }

            if (!yearInName.HasValue)
            {
                yearInName = movieYear;
            }

            if (result != null)
            {
                result.ExtractedName = nameWithoutYear;
                result.ExtractedYear = yearInName;
            }

            var movie = _libraryManager.GetItemList(new InternalItemsQuery
            {
                IncludeItemTypes = new[] { typeof(Movie).Name },
                Recursive = true,
                DtoOptions = new DtoOptions(true)
            })
                .Cast<Movie>()
                .Select(i => NameUtils.GetMatchScore(nameWithoutYear, yearInName, i))
                .Where(i => i.Item2 > 0)
                .OrderByDescending(i => i.Item2)
                .Select(i => i.Item1)
                .FirstOrDefault();

            return movie;
        }

        /// <summary>
        /// Gets the new path.
        /// </summary>
        /// <param name="sourcePath">The source path.</param>
        /// <param name="movie">The movie.</param>
        /// <param name="options">The options.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>System.String.</returns>
        private async Task<string> GetNewPath(string sourcePath,
            Movie movie,
            MovieFileOrganizationOptions options,
            CancellationToken cancellationToken)
        {
            var movieInfo = new MovieInfo
            {
                Name = movie.Name,
                MetadataCountryCode = movie.GetPreferredMetadataCountryCode(),
                MetadataLanguage = movie.GetPreferredMetadataLanguage(),
                ProviderIds = movie.ProviderIds,
            };

            var searchResults = await _providerManager.GetRemoteSearchResults<Movie, MovieInfo>(new RemoteSearchQuery<MovieInfo>
            {
                SearchInfo = movieInfo

            }, cancellationToken).ConfigureAwait(false);

            var searchedMovie = searchResults.FirstOrDefault();

            if (searchedMovie == null)
            {
                var msg = string.Format("No provider metadata found for {0}", movie.Name);
                _logger.Warn(msg);
                throw new Exception(msg);
            }

            var episodeFileName = GetMovieFileName(sourcePath, movie.Name, movie.ProductionYear, options);

            if (string.IsNullOrEmpty(episodeFileName))
            {
                // cause failure
                return string.Empty;
            }

            return episodeFileName;
        }

        private string GetMovieFileName(string sourcePath, string movieName, int? productionYear, MovieFileOrganizationOptions options)
        {
            movieName = _fileSystem.GetValidFilename(movieName).Trim();

            var sourceExtension = (Path.GetExtension(sourcePath) ?? string.Empty).TrimStart('.');

            var pattern = options.MoviePattern;

            if (string.IsNullOrWhiteSpace(pattern))
            {
                throw new Exception("GetEpisodeFileName: Configured episode name pattern is empty!");
            }

            var result = pattern.Replace("%mn", movieName)
                .Replace("%m.n", movieName.Replace(" ", "."))
                .Replace("%m_n", movieName.Replace(" ", "_"))
                .Replace("%my", productionYear.ToString())
                .Replace("%ext", sourceExtension)
                .Replace("%fn", Path.GetFileNameWithoutExtension(sourcePath));

            // Finally, call GetValidFilename again in case user customized the episode expression with any invalid filename characters
            return _fileSystem.GetValidFilename(result).Trim();
        }

        private bool IsSameEpisode(string sourcePath, string newPath)
        {
            try
            {
                var sourceFileInfo = _fileSystem.GetFileInfo(sourcePath);
                var destinationFileInfo = _fileSystem.GetFileInfo(newPath);

                if (sourceFileInfo.Length == destinationFileInfo.Length)
                {
                    return true;
                }
            }
            catch (FileNotFoundException)
            {
                return false;
            }
            catch (IOException)
            {
                return false;
            }

            return false;
        }
    }
}
