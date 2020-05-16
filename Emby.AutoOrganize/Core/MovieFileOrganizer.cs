using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Emby.AutoOrganize.Model;
using Emby.Naming.Common;
using Emby.Naming.Video;
using MediaBrowser.Controller.Dto;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.IO;
using MediaBrowser.Model.Providers;
using Microsoft.Extensions.Logging;

namespace Emby.AutoOrganize.Core
{
    /// <summary>
    /// Service used for organizing movie media files.
    /// </summary>
    public class MovieFileOrganizer
    {
        private readonly ILibraryMonitor _libraryMonitor;
        private readonly ILibraryManager _libraryManager;
        private readonly ILogger<MovieFileOrganizer> _logger;
        private readonly IFileSystem _fileSystem;
        private readonly IFileOrganizationService _organizationService;
        private readonly IProviderManager _providerManager;
        private NamingOptions _namingOptions;

        /// <summary>
        /// Initializes a new instance of the <see cref="MovieFileOrganizer"/> class.
        /// </summary>
        [SuppressMessage("StyleCop.CSharp.DocumentationRules", "SA1611:Element parameters should be documented", Justification = "Parameter types/names are self-documenting")]
        public MovieFileOrganizer(
            IFileOrganizationService organizationService,
            IFileSystem fileSystem,
            ILogger<MovieFileOrganizer> logger,
            ILibraryManager libraryManager,
            ILibraryMonitor libraryMonitor,
            IProviderManager providerManager)
        {
            _organizationService = organizationService;
            _fileSystem = fileSystem;
            _logger = logger;
            _libraryManager = libraryManager;
            _libraryMonitor = libraryMonitor;
            _providerManager = providerManager;
        }

        private FileOrganizerType CurrentFileOrganizerType => FileOrganizerType.Movie;

        /// <summary>
        /// Organize a movie media file.
        /// </summary>
        /// <param name="path">The filepath of the movie.</param>
        /// <param name="options">The organize options to use.</param>
        /// <param name="overwriteExisting">If true, any existing file at the same destination path will be overwritten.</param>
        /// <param name="cancellationToken">A cancellation token for the operation.</param>
        /// <returns>A task representing the operation completion and containing the operation result.</returns>
        public async Task<FileOrganizationResult> OrganizeMovieFile(
            string path,
            MovieFileOrganizationOptions options,
            bool overwriteExisting,
            CancellationToken cancellationToken)
        {
            _logger.LogInformation("Sorting file {0}", path);

            var result = new FileOrganizationResult
            {
                Date = DateTime.UtcNow,
                OriginalPath = path,
                OriginalFileName = Path.GetFileName(path),
                Type = FileOrganizerType.Unknown,
                FileSize = _fileSystem.GetFileInfo(path).Length
            };

            try
            {
                if (_libraryMonitor.IsPathLocked(path))
                {
                    result.Status = FileSortingStatus.Failure;
                    result.StatusMessage = "Path is locked by other processes. Please try again later.";
                    _logger.LogInformation("Auto-organize Path is locked by other processes. Please try again later.");
                    return result;
                }

                _namingOptions = _namingOptions ?? new NamingOptions();
                var resolver = new VideoResolver(_namingOptions);

                var movieInfo = resolver.Resolve(path, false) ??
                    new VideoFileInfo();

                var movieName = movieInfo.Name;

                if (!string.IsNullOrEmpty(movieName))
                {
                    var movieYear = movieInfo.Year;

                    _logger.LogDebug("Extracted information from {0}. Movie {1}, Year {2}", path, movieName, movieYear);

                    await OrganizeMovie(
                        path,
                        movieName,
                        movieYear,
                        options,
                        overwriteExisting,
                        result,
                        cancellationToken).ConfigureAwait(false);
                }
                else
                {
                    var msg = "Unable to determine movie name from " + path;
                    result.Status = FileSortingStatus.Failure;
                    result.StatusMessage = msg;
                    _logger.LogWarning(msg);
                }

                // Handle previous result
                var previousResult = _organizationService.GetResultBySourcePath(path);

                if ((previousResult != null && result.Type == FileOrganizerType.Unknown) || (previousResult?.Status == result.Status &&
                                                                                             previousResult?.StatusMessage == result.StatusMessage &&
                                                                                             result.Status != FileSortingStatus.Success))
                {
                    // Don't keep saving the same result over and over if nothing has changed
                    return previousResult;
                }
            }
            catch (Exception ex)
            {
                result.Status = FileSortingStatus.Failure;
                result.StatusMessage = ex.Message;
                _logger.LogError(ex, "Error organizing file {0}", path);
            }

            _organizationService.SaveResult(result, CancellationToken.None);

            return result;
        }

        private Movie CreateNewMovie(MovieFileOrganizationRequest request, FileOrganizationResult result, MovieFileOrganizationOptions options)
        {
            // To avoid Movie duplicate by mistake (Missing SmartMatch and wrong selection in UI)
            var movie = GetMatchingMovie(request.NewMovieName, request.NewMovieYear, request.TargetFolder, result);

            if (movie == null)
            {
                // We're having a new movie here
                movie = new Movie
                {
                    Id = Guid.NewGuid(),
                    Name = request.NewMovieName,
                    ProductionYear = request.NewMovieYear,
                    IsInMixedFolder = !options.MovieFolder,
                    ProviderIds = request.NewMovieProviderIds.ToDictionary(x => x.Key, x => x.Value),
                };

                var newPath = GetMoviePath(result.OriginalPath, movie, options);

                if (string.IsNullOrEmpty(newPath))
                {
                    var msg = $"Unable to sort {result.OriginalPath} because target path could not be determined.";
                    throw new OrganizationException(msg);
                }

                movie.Path = Path.Combine(request.TargetFolder, newPath);
            }

            return movie;
        }

        /// <summary>
        /// Organize a movie media file with user-supplied parameters.
        /// </summary>
        /// <param name="request">The user supplied parameters provided via API request.</param>
        /// <param name="options">The organize options to use.</param>
        /// <param name="cancellationToken">A cancellation token for the operation.</param>
        /// <returns>A task representing the operation completion and containing the operation result.</returns>
        public async Task<FileOrganizationResult> OrganizeWithCorrection(MovieFileOrganizationRequest request, MovieFileOrganizationOptions options, CancellationToken cancellationToken)
        {
            var result = _organizationService.GetResult(request.ResultId);

            try
            {
                Movie movie = null;

                if (request.NewMovieProviderIds.Count > 0)
                {
                    // To avoid Series duplicate by mistake (Missing SmartMatch and wrong selection in UI)
                    movie = CreateNewMovie(request, result, options);
                }

                if (movie == null)
                {
                    // Existing movie
                    movie = (Movie)_libraryManager.GetItemById(request.MovieId);
                }

                // We manually set the media as Movie
                result.Type = CurrentFileOrganizerType;

                await OrganizeMovie(
                    result.OriginalPath,
                    movie,
                    options,
                    true,
                    result,
                    cancellationToken).ConfigureAwait(false);

                _organizationService.SaveResult(result, CancellationToken.None);
            }
            catch (Exception ex)
            {
                result.Status = FileSortingStatus.Failure;
                result.StatusMessage = ex.Message;
            }

            return result;
        }

        private async Task OrganizeMovie(
            string sourcePath,
            string movieName,
            int? movieYear,
            MovieFileOrganizationOptions options,
            bool overwriteExisting,
            FileOrganizationResult result,
            CancellationToken cancellationToken)
        {
            var movie = GetMatchingMovie(movieName, movieYear, string.Empty, result);
            RemoteSearchResult searchResult = null;

            if (movie == null)
            {
                var autoResult = await AutoDetectMovie(movieName, movieYear, result, options, cancellationToken).ConfigureAwait(false);

                movie = autoResult?.Item1;
                searchResult = autoResult?.Item2;

                if (movie == null)
                {
                    var msg = "Unable to find movie in library matching name " + movieName;
                    result.Status = FileSortingStatus.Failure;
                    result.StatusMessage = msg;
                    _logger.LogWarning(msg);
                    return;
                }
            }

            // We detected an Movie (either auto-detect or in library)
            // We have all the chance that the media type is an Movie
            result.Type = CurrentFileOrganizerType;

            await OrganizeMovie(
                sourcePath,
                movie,
                options,
                overwriteExisting,
                result,
                cancellationToken).ConfigureAwait(false);
        }

        private Task OrganizeMovie(
            string sourcePath,
            Movie movie,
            MovieFileOrganizationOptions options,
            bool overwriteExisting,
            FileOrganizationResult result,
            CancellationToken cancellationToken)
        {
            _logger.LogInformation("Sorting file {0} into movie {1}", sourcePath, movie.Path);

            bool isNew = string.IsNullOrWhiteSpace(result.Id);

            if (isNew)
            {
                _organizationService.SaveResult(result, cancellationToken);
            }

            if (!_organizationService.AddToInProgressList(result, isNew))
            {
                throw new OrganizationException("File is currently processed otherwise. Please try again later.");
            }

            try
            {
                // Proceed to sort the file
                var newPath = movie.Path;
                _logger.LogInformation("Sorting file {0} to new path {1}", sourcePath, newPath);
                result.TargetPath = newPath;

                var fileExists = File.Exists(result.TargetPath);

                if (!overwriteExisting)
                {
                    if (options.CopyOriginalFile && fileExists && IsSameMovie(sourcePath, newPath))
                    {
                        var msg = $"File '{sourcePath}' already copied to new path '{newPath}', stopping organization";
                        _logger.LogInformation(msg);
                        result.Status = FileSortingStatus.SkippedExisting;
                        result.StatusMessage = msg;
                        return Task.CompletedTask;
                    }

                    if (fileExists)
                    {
                        var msg = $"File '{sourcePath}' already exists as '{newPath}', stopping organization";
                        _logger.LogInformation(msg);
                        result.Status = FileSortingStatus.SkippedExisting;
                        result.StatusMessage = msg;
                        result.TargetPath = newPath;
                        return Task.CompletedTask;
                    }
                }

                PerformFileSorting(options, result);
            }
            catch (OrganizationException ex)
            {
                result.Status = FileSortingStatus.Failure;
                result.StatusMessage = ex.Message;
            }
            catch (Exception ex)
            {
                result.Status = FileSortingStatus.Failure;
                result.StatusMessage = ex.Message;
                _logger.LogError(ex, "Caught a generic exception while organizing {0}", sourcePath);
            }
            finally
            {
                _organizationService.RemoveFromInprogressList(result);
            }

            return Task.CompletedTask;
        }

        private void PerformFileSorting(MovieFileOrganizationOptions options, FileOrganizationResult result)
        {
            // We should probably handle this earlier so that we never even make it this far
            if (string.Equals(result.OriginalPath, result.TargetPath, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            _libraryMonitor.ReportFileSystemChangeBeginning(result.TargetPath);

            Directory.CreateDirectory(Path.GetDirectoryName(result.TargetPath));

            var targetAlreadyExists = File.Exists(result.TargetPath);

            try
            {
                if (targetAlreadyExists || options.CopyOriginalFile)
                {
                    File.Copy(result.OriginalPath, result.TargetPath, true);
                }
                else
                {
                    File.Move(result.OriginalPath, result.TargetPath);
                }

                result.Status = FileSortingStatus.Success;
                result.StatusMessage = string.Empty;
            }
            catch (Exception ex)
            {
                var errorMsg = $"Failed to move file from {result.OriginalPath} to {result.TargetPath}: {ex.Message}";

                result.Status = FileSortingStatus.Failure;
                result.StatusMessage = errorMsg;
                _logger.LogError(ex, errorMsg);

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
                    _logger.LogError(ex, "Error deleting {0}", result.OriginalPath);
                }
            }
        }

        private async Task<Tuple<Movie, RemoteSearchResult>> AutoDetectMovie(string movieName, int? movieYear, FileOrganizationResult result, MovieFileOrganizationOptions options, CancellationToken cancellationToken)
        {
            if (options.AutoDetectMovie)
            {
                var parsedName = _libraryManager.ParseName(movieName);

                var yearInName = parsedName.Year;
                var nameWithoutYear = parsedName.Name;
                RemoteSearchResult finalResult = null;

                if (string.IsNullOrWhiteSpace(nameWithoutYear))
                {
                    nameWithoutYear = movieName;
                }

                if (!yearInName.HasValue)
                {
                    yearInName = movieYear;
                }

                // Perform remote search
                var movieInfo = new MovieInfo { Name = nameWithoutYear, Year = yearInName, };
                var searchResultsTask = await _providerManager.GetRemoteSearchResults<Movie, MovieInfo>(
                    new RemoteSearchQuery<MovieInfo> { SearchInfo = movieInfo },
                    cancellationToken).ConfigureAwait(false);

                // Group movies by name and year (if 2 movie with the exact same name, the same year ...)
                var groupedResult = searchResultsTask.GroupBy(
                    p => new { p.Name, p.ProductionYear },
                    p => p,
                    (key, g) => new { Key = key, Result = g.ToList() }).ToList();

                if (groupedResult.Count == 1)
                {
                    finalResult = groupedResult.First().Result.First();
                }
                else if (groupedResult.Count > 1)
                {
                    var filtredResult = groupedResult
                        .Select(i => new { Ref = i, Score = NameUtils.GetMatchScore(nameWithoutYear, yearInName, i.Key.Name, i.Key.ProductionYear) })
                        .Where(i => i.Score > 0)
                        .OrderByDescending(i => i.Score)
                        .Select(i => i.Ref)
                        .FirstOrDefault();
                    finalResult = filtredResult?.Result.First();
                }

                if (finalResult != null)
                {
                    // We are in the good position, we can create the item
                    var organizationRequest = new MovieFileOrganizationRequest
                    {
                        NewMovieName = finalResult.Name,
                        NewMovieProviderIds = finalResult.ProviderIds,
                        NewMovieYear = finalResult.ProductionYear,
                        TargetFolder = options.DefaultMovieLibraryPath
                    };

                    var movie = CreateNewMovie(organizationRequest, result, options);

                    return new Tuple<Movie, RemoteSearchResult>(movie, finalResult);
                }
            }

            return null;
        }

        private Movie GetMatchingMovie(string movieName, int? movieYear, string targetFolder, FileOrganizationResult result)
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

            result.ExtractedName = nameWithoutYear;
            result.ExtractedYear = yearInName;

            var movie = _libraryManager.GetItemList(new InternalItemsQuery
            {
                IncludeItemTypes = new[] { typeof(Movie).Name },
                Recursive = true,
                DtoOptions = new DtoOptions(true),
            })
                .Cast<Movie>()
                .Select(i => NameUtils.GetMatchScore(nameWithoutYear, yearInName, i))
                .Where(i => i.Item2 > 0)
                .OrderByDescending(i => i.Item2)
                .Select(i => i.Item1)
                .FirstOrDefault(m =>

                    // Check For the right folder AND the right extension (to handle quality upgrade)
                    m.Path.StartsWith(targetFolder, StringComparison.Ordinal)
                    && Path.GetExtension(m.Path) == Path.GetExtension(result.OriginalPath));

            return movie;
        }

        /// <summary>
        /// Gets the new path.
        /// </summary>
        /// <param name="sourcePath">The source path.</param>
        /// <param name="movie">The movie.</param>
        /// <param name="options">The options.</param>
        /// <returns>System.String.</returns>
        private string GetMoviePath(
            string sourcePath,
            Movie movie,
            MovieFileOrganizationOptions options)
        {
            var movieFileName = string.Empty;

            if (options.MovieFolder)
            {
                movieFileName = Path.Combine(movieFileName, GetMovieFolder(sourcePath, movie, options));
            }

            movieFileName = Path.Combine(movieFileName, GetMovieFileName(sourcePath, movie, options));

            if (string.IsNullOrEmpty(movieFileName))
            {
                // cause failure
                return string.Empty;
            }

            return movieFileName;
        }

        private string GetMovieFileName(string sourcePath, Movie movie, MovieFileOrganizationOptions options)
        {
            return GetMovieNameInternal(sourcePath, movie, options.MoviePattern);
        }

        private string GetMovieFolder(string sourcePath, Movie movie, MovieFileOrganizationOptions options)
        {
            return GetMovieNameInternal(sourcePath, movie, options.MovieFolderPattern);
        }

        private string GetMovieNameInternal(string sourcePath, Movie movie, string pattern)
        {
            var movieName = _fileSystem.GetValidFilename(movie.Name).Trim();
            var productionYear = movie.ProductionYear;

            var sourceExtension = (Path.GetExtension(sourcePath) ?? string.Empty).TrimStart('.');

            if (string.IsNullOrWhiteSpace(pattern))
            {
                throw new OrganizationException("GetMovieFolder: Configured movie name pattern is empty!");
            }

            var result = pattern.Replace("%mn", movieName)
                .Replace("%m.n", movieName.Replace(" ", "."))
                .Replace("%m_n", movieName.Replace(" ", "_"))
                .Replace("%my", productionYear.ToString())
                .Replace("%ext", sourceExtension)
                .Replace("%fn", Path.GetFileNameWithoutExtension(sourcePath));

            // Finally, call GetValidFilename again in case user customized the movie expression with any invalid filename characters
            return _fileSystem.GetValidFilename(result).Trim();
        }

        private bool IsSameMovie(string sourcePath, string newPath)
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
