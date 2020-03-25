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
using Emby.Naming.TV;
using MediaBrowser.Controller.Dto;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Extensions;
using MediaBrowser.Model.IO;
using MediaBrowser.Model.Providers;
using Microsoft.Extensions.Logging;
using EpisodeInfo = MediaBrowser.Controller.Providers.EpisodeInfo;

namespace Emby.AutoOrganize.Core
{
    /// <summary>
    /// Service to use for organizing episode media files.
    /// </summary>
    public class EpisodeFileOrganizer
    {
        private readonly ILibraryMonitor _libraryMonitor;
        private readonly ILibraryManager _libraryManager;
        private readonly ILogger _logger;
        private readonly IFileSystem _fileSystem;
        private readonly IFileOrganizationService _organizationService;
        private readonly IProviderManager _providerManager;

        private readonly CultureInfo _usCulture = new CultureInfo("en-US");

        private NamingOptions _namingOptions;

        /// <summary>
        /// Initializes a new instance of the <see cref="EpisodeFileOrganizer"/> class.
        /// </summary>
        [SuppressMessage("StyleCop.CSharp.DocumentationRules", "SA1611:Element parameters should be documented", Justification = "Parameter types/names are self-documenting")]
        public EpisodeFileOrganizer(
            IFileOrganizationService organizationService,
            IFileSystem fileSystem,
            ILogger logger,
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

        private FileOrganizerType CurrentFileOrganizerType => FileOrganizerType.Episode;

        private NamingOptions GetNamingOptionsInternal()
        {
            _namingOptions = _namingOptions ?? new NamingOptions();
            return _namingOptions;
        }

        /// <summary>
        /// Organize an episode file.
        /// </summary>
        /// <param name="path">The path to the episode file.</param>
        /// <param name="options">The options to use for organizing the file.</param>
        /// <param name="cancellationToken">Cancellation token for the operation.</param>
        /// <returns>A task representing the file organization operation and containing the operation result.</returns>
        public async Task<FileOrganizationResult> OrganizeEpisodeFile(
            string path,
            TvFileOrganizationOptions options,
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

                var namingOptions = GetNamingOptionsInternal();
                var resolver = new EpisodeResolver(namingOptions);

                var episodeInfo = resolver.Resolve(path, false) ??
                    new Naming.TV.EpisodeInfo();

                var seriesName = episodeInfo.SeriesName;
                int? seriesYear = null;

                if (!string.IsNullOrEmpty(seriesName))
                {
                    var seriesParseResult = _libraryManager.ParseName(seriesName);

                    seriesName = seriesParseResult.Name;
                    seriesYear = seriesParseResult.Year;
                }

                if (string.IsNullOrWhiteSpace(seriesName))
                {
                    seriesName = episodeInfo.SeriesName;
                }

                if (!string.IsNullOrEmpty(seriesName))
                {
                    var seasonNumber = episodeInfo.SeasonNumber;

                    result.ExtractedSeasonNumber = seasonNumber;

                    // Passing in true will include a few extra regex's
                    var episodeNumber = episodeInfo.EpisodeNumber;

                    result.ExtractedEpisodeNumber = episodeNumber;

                    var premiereDate = episodeInfo.IsByDate ?
                        new DateTime(episodeInfo.Year.Value, episodeInfo.Month.Value, episodeInfo.Day.Value) :
                        (DateTime?)null;

                    if (episodeInfo.IsByDate || (seasonNumber.HasValue && episodeNumber.HasValue))
                    {
                        if (episodeInfo.IsByDate)
                        {
                            _logger.LogDebug("Extracted information from {0}. Series name {1}, Date {2}", path, seriesName, premiereDate.Value);
                        }
                        else
                        {
                            _logger.LogDebug("Extracted information from {0}. Series name {1}, Season {2}, Episode {3}", path, seriesName, seasonNumber, episodeNumber);
                        }

                        // We detected an airdate or (an season number and an episode number)
                        // We have all the chance that the media type is an Episode
                        // if an earlier result exist with an different type, we update it
                        result.Type = CurrentFileOrganizerType;

                        var endingEpisodeNumber = episodeInfo.EndingEpsiodeNumber;

                        result.ExtractedEndingEpisodeNumber = endingEpisodeNumber;

                        await OrganizeEpisode(
                            path,
                            seriesName,
                            seriesYear,
                            seasonNumber,
                            episodeNumber,
                            endingEpisodeNumber,
                            premiereDate,
                            options,
                            false,
                            result,
                            cancellationToken).ConfigureAwait(false);
                    }
                    else
                    {
                        var msg = "Unable to determine episode number from " + path;
                        result.Status = FileSortingStatus.Failure;
                        result.StatusMessage = msg;
                        _logger.LogWarning(msg);
                    }
                }
                else
                {
                    var msg = "Unable to determine series name from " + path;
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
            catch (OrganizationException ex)
            {
                result.Status = FileSortingStatus.Failure;
                result.StatusMessage = ex.Message;
            }
            catch (Exception ex)
            {
                result.Status = FileSortingStatus.Failure;
                result.StatusMessage = ex.Message;
                _logger.LogError(ex, "Error organizing file");
            }

            _organizationService.SaveResult(result, CancellationToken.None);

            return result;
        }

        private async Task<Series> AutoDetectSeries(
            string seriesName,
            int? seriesYear,
            TvFileOrganizationOptions options,
            CancellationToken cancellationToken)
        {
            if (options.AutoDetectSeries)
            {
                RemoteSearchResult finalResult = null;

                // Perform remote search
                var seriesInfo = new SeriesInfo { Name = seriesName, Year = seriesYear };
                var searchQuery = new RemoteSearchQuery<SeriesInfo> { SearchInfo = seriesInfo };
                var searchResults = await _providerManager.GetRemoteSearchResults<Series, SeriesInfo>(searchQuery, cancellationToken).ConfigureAwait(false);

                // Group series by name and year (if 2 series with the exact same name, the same year ...)
                var groupedResult = searchResults.GroupBy(
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
                        .Select(i => new { Ref = i, Score = NameUtils.GetMatchScore(seriesName, seriesYear, i.Key.Name, i.Key.ProductionYear) })
                        .Where(i => i.Score > 0)
                        .OrderByDescending(i => i.Score)
                        .Select(i => i.Ref)
                        .FirstOrDefault();
                    finalResult = filtredResult?.Result.First();
                }

                if (finalResult != null)
                {
                    // We are in the good position, we can create the item
                    var organizationRequest = new EpisodeFileOrganizationRequest
                    {
                        NewSeriesName = finalResult.Name,
                        NewSeriesProviderIds = finalResult.ProviderIds,
                        NewSeriesYear = finalResult.ProductionYear,
                        TargetFolder = options.DefaultSeriesLibraryPath
                    };

                    return await CreateNewSeries(organizationRequest, finalResult, options, cancellationToken).ConfigureAwait(false);
                }
            }

            return null;
        }

        private async Task<Series> CreateNewSeries(
            EpisodeFileOrganizationRequest request,
            RemoteSearchResult result,
            TvFileOrganizationOptions options,
            CancellationToken cancellationToken)
        {
            Series series;

            // Ensure that we don't create the same series multiple times; create one at a time
            var seriesCreationLock = new object();
            lock (seriesCreationLock)
            {
                series = GetMatchingSeries(request.NewSeriesName, request.NewSeriesYear, request.TargetFolder, null);

                if (series == null)
                {
                    // We're having a new series here

                    series = new Series
                    {
                        Id = Guid.NewGuid(),
                        Name = request.NewSeriesName,
                        ProductionYear = request.NewSeriesYear
                    };

                    var seriesFolderName = GetSeriesDirectoryName(series, options);

                    series.Path = Path.Combine(request.TargetFolder, seriesFolderName);

                    // Create the folder
                    Directory.CreateDirectory(series.Path);

                    series.ProviderIds = request.NewSeriesProviderIds.ToDictionary(x => x.Key, x => x.Value);
                }
            }

            // async outside of the lock for perfs
            var refreshOptions = new MetadataRefreshOptions(new DirectoryService(_fileSystem))
            {
                SearchResult = result
            };
            await series.RefreshMetadata(refreshOptions, cancellationToken).ConfigureAwait(false);

            return series;
        }

        /// <summary>
        /// Organize an episode file with parameters provided by the end-user.
        /// </summary>
        /// <param name="request">The parameters provided by the user via API request.</param>
        /// <param name="options">The organization options to use.</param>
        /// <param name="cancellationToken">A cancellation token for the operation.</param>
        /// <returns>A task representing the organization operation and containing the operation result.</returns>
        public async Task<FileOrganizationResult> OrganizeWithCorrection(
            EpisodeFileOrganizationRequest request,
            TvFileOrganizationOptions options,
            CancellationToken cancellationToken)
        {
            var result = _organizationService.GetResult(request.ResultId);

            try
            {
                Series series = null;

                if (request.NewSeriesProviderIds.Count > 0)
                {
                    series = await CreateNewSeries(request, null, options, cancellationToken).ConfigureAwait(false);
                }

                if (series == null)
                {
                    // Existing Series
                    series = (Series)_libraryManager.GetItemById(request.SeriesId);
                }

                // We manually set the media as Series
                result.Type = CurrentFileOrganizerType;

                await OrganizeEpisode(
                    result.OriginalPath,
                    series,
                    request.SeasonNumber,
                    request.EpisodeNumber,
                    request.EndingEpisodeNumber,
                    null,
                    options,
                    request.RememberCorrection,
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

        private Task OrganizeEpisode(
            string sourcePath,
            string seriesName,
            int? seriesYear,
            int? seasonNumber,
            int? episodeNumber,
            int? endingEpiosdeNumber,
            DateTime? premiereDate,
            TvFileOrganizationOptions options,
            bool rememberCorrection,
            FileOrganizationResult result,
            CancellationToken cancellationToken)
        {
            var series = GetMatchingSeries(seriesName, seriesYear, string.Empty, result);

            if (series == null)
            {
                series = AutoDetectSeries(seriesName, null, options, cancellationToken).Result;

                if (series == null)
                {
                    var msg = "Unable to find series in library matching name " + seriesName;
                    result.Status = FileSortingStatus.Failure;
                    result.StatusMessage = msg;
                    _logger.LogWarning(msg);
                    return Task.FromResult(true);
                }
            }

            return OrganizeEpisode(
                sourcePath,
                series,
                seasonNumber,
                episodeNumber,
                endingEpiosdeNumber,
                premiereDate,
                options,
                rememberCorrection,
                result,
                cancellationToken);
        }

        /// <summary>
        /// Organize part responsible of Season AND Episode recognition.
        /// </summary>
        private async Task OrganizeEpisode(
            string sourcePath,
            Series series,
            int? seasonNumber,
            int? episodeNumber,
            int? endingEpiosdeNumber,
            DateTime? premiereDate,
            TvFileOrganizationOptions options,
            bool rememberCorrection,
            FileOrganizationResult result,
            CancellationToken cancellationToken)
        {
            var episode = await GetMatchingEpisode(
                series,
                seasonNumber,
                episodeNumber,
                endingEpiosdeNumber,
                result,
                premiereDate,
                cancellationToken).ConfigureAwait(false);

            Season season;
            season = !string.IsNullOrEmpty(episode.Season?.Path)
                ? episode.Season
                : GetMatchingSeason(series, episode, options);

            // Now we can check the episode Path
            if (string.IsNullOrEmpty(episode.Path))
            {
                SetEpisodeFileName(sourcePath, series, season, episode, options);
            }

            await OrganizeEpisode(
                sourcePath,
                series,
                episode,
                options,
                rememberCorrection,
                result,
                cancellationToken).ConfigureAwait(false);
        }

        private Task OrganizeEpisode(
            string sourcePath,
            Series series,
            Episode episode,
            TvFileOrganizationOptions options,
            bool rememberCorrection,
            FileOrganizationResult result,
            CancellationToken cancellationToken)
        {
            _logger.LogInformation("Sorting file {0} into series {1}", sourcePath, series.Path);

            var originalExtractedSeriesString = result.ExtractedName;

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
                var newPath = episode.Path;

                if (string.IsNullOrEmpty(newPath))
                {
                    var msg = $"Unable to sort {sourcePath} because target path could not be determined.";
                    throw new OrganizationException(msg);
                }

                _logger.LogInformation("Sorting file {0} to new path {1}", sourcePath, newPath);
                result.TargetPath = newPath;

                var fileExists = File.Exists(result.TargetPath);
                var otherDuplicatePaths = GetOtherDuplicatePaths(result.TargetPath, series, episode);

                if (!options.OverwriteExistingEpisodes)
                {
                    if (options.CopyOriginalFile && fileExists && IsSameEpisode(sourcePath, newPath))
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

                    if (otherDuplicatePaths.Count > 0)
                    {
                        var msg = $"File '{sourcePath}' already exists as these:'{string.Join("', '", otherDuplicatePaths)}'. Stopping organization";
                        _logger.LogInformation(msg);
                        result.Status = FileSortingStatus.SkippedExisting;
                        result.StatusMessage = msg;
                        result.DuplicatePaths = otherDuplicatePaths;
                        return Task.CompletedTask;
                    }
                }

                PerformFileSorting(options, result);

                if (options.OverwriteExistingEpisodes)
                {
                    var hasRenamedFiles = false;

                    foreach (var path in otherDuplicatePaths)
                    {
                        _logger.LogDebug("Removing duplicate episode {0}", path);

                        _libraryMonitor.ReportFileSystemChangeBeginning(path);

                        var renameRelatedFiles = !hasRenamedFiles &&
                            string.Equals(Path.GetDirectoryName(path), Path.GetDirectoryName(result.TargetPath), StringComparison.OrdinalIgnoreCase);

                        if (renameRelatedFiles)
                        {
                            hasRenamedFiles = true;
                        }

                        try
                        {
                            DeleteLibraryFile(path, renameRelatedFiles, result.TargetPath);
                        }
                        catch (IOException ex)
                        {
                            _logger.LogError(ex, "Error removing duplicate episode: {0}", path);
                        }
                        finally
                        {
                            _libraryMonitor.ReportFileSystemChangeComplete(path, true);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                result.Status = FileSortingStatus.Failure;
                result.StatusMessage = ex.Message;
                _logger.LogError(ex, "Caught a generic exception while organizing an episode");
                return Task.CompletedTask;
            }
            finally
            {
                _organizationService.RemoveFromInprogressList(result);
            }

            if (rememberCorrection)
            {
                SaveSmartMatchString(originalExtractedSeriesString, series, cancellationToken);
            }

            return Task.CompletedTask;
        }

        private void SaveSmartMatchString(string matchString, Series series, CancellationToken cancellationToken)
        {
            if (string.IsNullOrEmpty(matchString) || matchString.Length < 3)
            {
                return;
            }

            var info = _organizationService.GetSmartMatchInfos().Items.FirstOrDefault(i => string.Equals(i.ItemName, series.Name, StringComparison.OrdinalIgnoreCase));

            if (info == null)
            {
                info = new SmartMatchResult
                {
                    ItemName = series.Name,
                    OrganizerType = CurrentFileOrganizerType,
                    DisplayName = series.Name
                };
            }

            if (!info.MatchStrings.Contains(matchString, StringComparer.OrdinalIgnoreCase))
            {
                info.MatchStrings.Add(matchString);
                _organizationService.SaveResult(info, cancellationToken);
            }
        }

        private void DeleteLibraryFile(string path, bool renameRelatedFiles, string targetPath)
        {
            _fileSystem.DeleteFile(path);

            if (!renameRelatedFiles)
            {
                return;
            }

            // Now find other files
            var originalFilenameWithoutExtension = Path.GetFileNameWithoutExtension(path);
            var directory = Path.GetDirectoryName(path);

            if (!string.IsNullOrWhiteSpace(originalFilenameWithoutExtension) && !string.IsNullOrWhiteSpace(directory))
            {
                // Get all related files, e.g. metadata, images, etc
                var files = _fileSystem.GetFilePaths(directory)
                    .Where(i => (Path.GetFileNameWithoutExtension(i) ?? string.Empty).StartsWith(originalFilenameWithoutExtension, StringComparison.OrdinalIgnoreCase))
                    .ToList();

                var targetFilenameWithoutExtension = Path.GetFileNameWithoutExtension(targetPath);

                foreach (var file in files)
                {
                    directory = Path.GetDirectoryName(file);
                    var filename = Path.GetFileName(file);

                    filename = filename.Replace(originalFilenameWithoutExtension, targetFilenameWithoutExtension, StringComparison.OrdinalIgnoreCase);

                    var destination = Path.Combine(directory, filename);

                    File.Move(file, destination);
                }
            }
        }

        private List<string> GetOtherDuplicatePaths(
            string targetPath,
            Series series,
            Episode episode)
        {
            // TODO: Support date-naming?
            if (!series.ParentIndexNumber.HasValue || !episode.IndexNumber.HasValue)
            {
                return new List<string>();
            }

            var episodePaths = series.GetRecursiveChildren()
                .OfType<Episode>()
                .Where(i =>
                {
                    var locationType = i.LocationType;

                    // Must be file system based and match exactly
                    if (locationType != LocationType.Remote &&
                        locationType != LocationType.Virtual &&
                        i.ParentIndexNumber.HasValue &&
                        i.ParentIndexNumber.Value == series.ParentIndexNumber &&
                        i.IndexNumber.HasValue &&
                        i.IndexNumber.Value == episode.IndexNumber)
                    {
                        if (episode.IndexNumberEnd.HasValue || i.IndexNumberEnd.HasValue)
                        {
                            return episode.IndexNumberEnd.HasValue && i.IndexNumberEnd.HasValue &&
                                   episode.IndexNumberEnd.Value == i.IndexNumberEnd.Value;
                        }

                        return true;
                    }

                    return false;
                })
                .Select(i => i.Path)
                .ToList();

            var folder = Path.GetDirectoryName(targetPath);
            var targetFileNameWithoutExtension = Path.GetFileNameWithoutExtension(targetPath);

            try
            {
                var filesOfOtherExtensions = _fileSystem.GetFilePaths(folder)
                    .Where(i => _libraryManager.IsVideoFile(i) && string.Equals(Path.GetFileNameWithoutExtension(i), targetFileNameWithoutExtension, StringComparison.OrdinalIgnoreCase));

                episodePaths.AddRange(filesOfOtherExtensions);
            }
            catch (IOException)
            {
                // No big deal. Maybe the season folder doesn't already exist.
            }

            return episodePaths.Where(i => !string.Equals(i, targetPath, StringComparison.OrdinalIgnoreCase))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private void PerformFileSorting(TvFileOrganizationOptions options, FileOrganizationResult result)
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

        private async Task<Episode> GetMatchingEpisode(
            Series series,
            int? seasonNumber,
            int? episodeNumber,
            int? endingEpiosdeNumber,
            FileOrganizationResult result,
            DateTime? premiereDate,
            CancellationToken cancellationToken)
        {
            var episode = series
                .GetRecursiveChildren().OfType<Episode>()
                .FirstOrDefault(e => e.ParentIndexNumber == seasonNumber
                        && e.IndexNumber == episodeNumber
                        && e.IndexNumberEnd == endingEpiosdeNumber
                        && e.LocationType == LocationType.FileSystem
                        && Path.GetExtension(e.Path) == Path.GetExtension(result.OriginalPath));

            if (episode == null)
            {
                return await CreateNewEpisode(series, seasonNumber, episodeNumber, endingEpiosdeNumber, premiereDate, cancellationToken).ConfigureAwait(false);
            }

            return episode;
        }

        private Season GetMatchingSeason(Series series, Episode episode, TvFileOrganizationOptions options)
        {
            var season = episode.Season;

            if (season == null)
            {
                season = series
                    .GetRecursiveChildren().OfType<Season>()
                    .FirstOrDefault(e => e.IndexNumber == episode.ParentIndexNumber
                                         && e.LocationType == LocationType.FileSystem);

                if (season == null)
                {
                    if (!episode.ParentIndexNumber.HasValue)
                    {
                        var msg = $"No season found for {series.Name} season {episode.ParentIndexNumber} episode {episode.IndexNumber}.";
                        _logger.LogWarning(msg);
                        throw new OrganizationException(msg);
                    }

                    season = new Season
                    {
                        Id = Guid.NewGuid(),
                        SeriesId = series.Id,
                        IndexNumber = episode.ParentIndexNumber,
                    };
                }
            }

            // If the season path is missing, compute it and create the directory on the filesystem
            if (string.IsNullOrEmpty(season.Path))
            {
                season.Path = GetSeasonFolderPath(series, episode.ParentIndexNumber.Value, options);
                Directory.CreateDirectory(season.Path);
            }

            return season;
        }

        private Series GetMatchingSeries(string seriesName, int? seriesYear, string targetFolder, FileOrganizationResult result)
        {
            if (result != null)
            {
                result.ExtractedName = seriesName;
                result.ExtractedYear = seriesYear;
            }

            var series = _libraryManager.GetItemList(new InternalItemsQuery
            {
                IncludeItemTypes = new[] { typeof(Series).Name },
                Recursive = true,
                DtoOptions = new DtoOptions(true)
            })
                .Cast<Series>()
                .Select(i => NameUtils.GetMatchScore(seriesName, seriesYear, i))
                .Where(i => i.Item2 > 0)
                .OrderByDescending(i => i.Item2)
                .Select(i => i.Item1)
                .FirstOrDefault(s => s.Path.StartsWith(targetFolder, StringComparison.Ordinal));

            if (series == null)
            {
                var info = _organizationService.GetSmartMatchInfos().Items.FirstOrDefault(e => e.MatchStrings.Contains(seriesName, StringComparer.OrdinalIgnoreCase));

                if (info != null)
                {
                    series = _libraryManager.GetItemList(new InternalItemsQuery
                    {
                        IncludeItemTypes = new[] { typeof(Series).Name },
                        Recursive = true,
                        Name = info.ItemName,
                        DtoOptions = new DtoOptions(true)
                    }).Cast<Series>().FirstOrDefault(s => s.Path.StartsWith(targetFolder, StringComparison.Ordinal));
                }
            }

            return series;
        }

        /// <summary>
        /// Get the new series name.
        /// </summary>
        private string GetSeriesDirectoryName(Series series, TvFileOrganizationOptions options)
        {
            var seriesName = series.Name;
            var serieYear = series.ProductionYear;
            var seriesFullName = seriesName;
            if (series.ProductionYear.HasValue)
            {
                seriesFullName = $"{seriesFullName} ({series.ProductionYear})";
            }

            var seasonFolderName = options.SeriesFolderPattern.
                Replace("%sn", seriesName)
                .Replace("%s.n", seriesName.Replace(" ", "."))
                .Replace("%s_n", seriesName.Replace(" ", "_"))
                .Replace("%sy", serieYear.ToString())
                .Replace("%fn", seriesFullName);

            return _fileSystem.GetValidFilename(seasonFolderName);
        }

        /// <summary>
        /// Look up metadata for an episode and use it to create an <see cref="Episode"/> object.
        /// </summary>
        /// <param name="series">The series the episode is in.</param>
        /// <param name="seasonNumber">The season number the episode is in.</param>
        /// <param name="episodeNumber">The episode number.</param>
        /// <param name="endingEpisodeNumber">The ending episode number.</param>
        /// <param name="premiereDate">The premiere date.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>A task representing the creation of the <see cref="Episode"/> object.</returns>
        /// <exception cref="OrganizationException">If no metadata can be found for the specified episode parameters.</exception>
        private async Task<Episode> CreateNewEpisode(
            Series series,
            int? seasonNumber,
            int? episodeNumber,
            int? endingEpisodeNumber,
            DateTime? premiereDate,
            CancellationToken cancellationToken)
        {
            var episodeInfo = new EpisodeInfo
            {
                IndexNumber = episodeNumber,
                IndexNumberEnd = endingEpisodeNumber,
                MetadataCountryCode = series.GetPreferredMetadataCountryCode(),
                MetadataLanguage = series.GetPreferredMetadataLanguage(),
                ParentIndexNumber = seasonNumber,
                SeriesProviderIds = series.ProviderIds,
                PremiereDate = premiereDate
            };

            var searchResults = await _providerManager.GetRemoteSearchResults<Episode, EpisodeInfo>(
                new RemoteSearchQuery<EpisodeInfo> { SearchInfo = episodeInfo },
                cancellationToken).ConfigureAwait(false);

            var episodeSearch = searchResults.FirstOrDefault();

            if (episodeSearch == null)
            {
                var msg = $"No provider metadata found for {series.Name} season {seasonNumber} episode {episodeNumber}";
                _logger.LogWarning(msg);
                throw new OrganizationException(msg);
            }

            seasonNumber = seasonNumber ?? episodeSearch.ParentIndexNumber;
            episodeNumber = episodeNumber ?? episodeSearch.IndexNumber;
            endingEpisodeNumber = endingEpisodeNumber ?? episodeSearch.IndexNumberEnd;

            var episode = new Episode
            {
                ParentIndexNumber = seasonNumber,
                SeriesId = series.Id,
                IndexNumber = episodeNumber,
                IndexNumberEnd = endingEpisodeNumber,
                ProviderIds = episodeSearch.ProviderIds,
                Name = episodeSearch.Name,
            };

            return episode;
        }

        /// <summary>
        /// Gets the season folder path.
        /// </summary>
        /// <param name="series">The series.</param>
        /// <param name="seasonNumber">The season number.</param>
        /// <param name="options">The options.</param>
        /// <returns>System.String.</returns>
        private string GetSeasonFolderPath(Series series, int seasonNumber, TvFileOrganizationOptions options)
        {
            var path = series.Path;

            if (ContainsEpisodesWithoutSeasonFolders(series))
            {
                return path;
            }

            if (seasonNumber == 0)
            {
                return Path.Combine(path, _fileSystem.GetValidFilename(options.SeasonZeroFolderName));
            }

            var seasonFolderName = options.SeasonFolderPattern
                .Replace("%s", seasonNumber.ToString(_usCulture))
                .Replace("%0s", seasonNumber.ToString("00", _usCulture))
                .Replace("%00s", seasonNumber.ToString("000", _usCulture));

            return Path.Combine(path, _fileSystem.GetValidFilename(seasonFolderName));
        }

        private bool ContainsEpisodesWithoutSeasonFolders(Series series)
        {
            var children = series.Children;
            foreach (var child in children)
            {
                if (child is Video)
                {
                    return true;
                }
            }

            return false;
        }

        private void SetEpisodeFileName(string sourcePath, Series series, Season season, Episode episode, TvFileOrganizationOptions options)
        {
            var seriesName = _fileSystem.GetValidFilename(series.Name).Trim();

            var episodeTitle = _fileSystem.GetValidFilename(episode.Name).Trim();

            if (!episode.IndexNumber.HasValue || !season.IndexNumber.HasValue)
            {
                throw new OrganizationException("GetEpisodeFileName: Mandatory param as missing!");
            }

            var endingEpisodeNumber = episode.IndexNumberEnd;
            var episodeNumber = episode.IndexNumber.Value;
            var seasonNumber = season.IndexNumber.Value;

            var sourceExtension = (Path.GetExtension(sourcePath) ?? string.Empty).TrimStart('.');

            var pattern = endingEpisodeNumber.HasValue ? options.MultiEpisodeNamePattern : options.EpisodeNamePattern;

            if (string.IsNullOrWhiteSpace(pattern))
            {
                throw new OrganizationException("GetEpisodeFileName: Configured episode name pattern is empty!");
            }

            var result = pattern.Replace("%sn", seriesName)
                .Replace("%s.n", seriesName.Replace(" ", "."))
                .Replace("%s_n", seriesName.Replace(" ", "_"))
                .Replace("%s", seasonNumber.ToString(_usCulture))
                .Replace("%0s", seasonNumber.ToString("00", _usCulture))
                .Replace("%00s", seasonNumber.ToString("000", _usCulture))
                .Replace("%ext", sourceExtension)
                .Replace("%en", "%#1")
                .Replace("%e.n", "%#2")
                .Replace("%e_n", "%#3")
                .Replace("%fn", Path.GetFileNameWithoutExtension(sourcePath));

            if (endingEpisodeNumber.HasValue)
            {
                result = result.Replace("%ed", endingEpisodeNumber.Value.ToString(_usCulture))
                .Replace("%0ed", endingEpisodeNumber.Value.ToString("00", _usCulture))
                .Replace("%00ed", endingEpisodeNumber.Value.ToString("000", _usCulture));
            }

            result = result.Replace("%e", episodeNumber.ToString(_usCulture))
                .Replace("%0e", episodeNumber.ToString("00", _usCulture))
                .Replace("%00e", episodeNumber.ToString("000", _usCulture));

            if (result.Contains("%#"))
            {
                result = result.Replace("%#1", episodeTitle)
                    .Replace("%#2", episodeTitle.Replace(" ", "."))
                    .Replace("%#3", episodeTitle.Replace(" ", "_"));
            }

            // Finally, call GetValidFilename again in case user customized the episode expression with any invalid filename characters
            episode.Path = Path.Combine(season.Path, _fileSystem.GetValidFilename(result).Trim());
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
