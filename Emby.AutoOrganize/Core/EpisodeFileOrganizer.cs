using MediaBrowser.Controller.Configuration;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Extensions;
using MediaBrowser.Model.Logging;
using System;
using System.Collections.Generic;
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
using Emby.Naming.TV;
using MediaBrowser.Model.Providers;
using EpisodeInfo = MediaBrowser.Controller.Providers.EpisodeInfo;

namespace Emby.AutoOrganize.Core
{
    public class EpisodeFileOrganizer
    {
        private readonly ILibraryMonitor _libraryMonitor;
        private readonly ILibraryManager _libraryManager;
        private readonly ILogger _logger;
        private readonly IFileSystem _fileSystem;
        private readonly IFileOrganizationService _organizationService;
        private readonly IServerConfigurationManager _config;
        private readonly IProviderManager _providerManager;

        private readonly CultureInfo _usCulture = new CultureInfo("en-US");

        public EpisodeFileOrganizer(IFileOrganizationService organizationService, IServerConfigurationManager config, IFileSystem fileSystem, ILogger logger, ILibraryManager libraryManager, ILibraryMonitor libraryMonitor, IProviderManager providerManager)
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
                var options = new NamingOptions();

                _namingOptions = options;
            }

            return _namingOptions;
        }

        public async Task<FileOrganizationResult> OrganizeEpisodeFile(
            string path,
            TvFileOrganizationOptions options,
            CancellationToken cancellationToken)
        {
            _logger.Info("Sorting file {0}", path);

            var result = new FileOrganizationResult
            {
                Date = DateTime.UtcNow,
                OriginalPath = path,
                OriginalFileName = Path.GetFileName(path),
                Type = FileOrganizerType.Episode,
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

                var forceEpisodeType = false;

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
                            _logger.Debug("Extracted information from {0}. Series name {1}, Date {2}", path, seriesName, premiereDate.Value);
                        }
                        else
                        {
                            _logger.Debug("Extracted information from {0}. Series name {1}, Season {2}, Episode {3}", path, seriesName, seasonNumber, episodeNumber);
                        }

                        // We detected an airdate or (an season number and an episode number)
                        // We have all the chance that the media type is an Episode
                        // if an earlier result exist with an different type, we update it
                        forceEpisodeType = true;

                        var endingEpisodeNumber = episodeInfo.EndingEpsiodeNumber;

                        result.ExtractedEndingEpisodeNumber = endingEpisodeNumber;

                        await OrganizeEpisode(path,
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
                        var msg = string.Format("Unable to determine episode number from {0}", path);
                        result.Status = FileSortingStatus.Failure;
                        result.StatusMessage = msg;
                        _logger.Warn(msg);
                    }
                }
                else
                {
                    var msg = string.Format("Unable to determine series name from {0}", path);
                    result.Status = FileSortingStatus.Failure;
                    result.StatusMessage = msg;
                    _logger.Warn(msg);
                }

                var previousResult = _organizationService.GetResultBySourcePath(path);

                if (previousResult != null)
                {
                    // if an earlier result exist with an different type, an we are pretty sure it was an Episode, we update it
                    if (previousResult.Type == FileOrganizerType.Episode || !forceEpisodeType)
                    {
                        // Don't keep saving the same result over and over if nothing has changed
                        if (previousResult.Status == result.Status && previousResult.StatusMessage == result.StatusMessage && result.Status != FileSortingStatus.Success)
                        {
                            return previousResult;
                        }
                    }
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
                _logger.ErrorException("Error organizing file", ex);
            }

            await _organizationService.SaveResult(result, CancellationToken.None).ConfigureAwait(false);

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

                #region Search One

                var seriesInfo = new SeriesInfo
                {
                    Name = seriesName,
                    Year = seriesYear
                };

                var searchResultsTask = await _providerManager.GetRemoteSearchResults<Series, SeriesInfo>(new RemoteSearchQuery<SeriesInfo>
                {
                    SearchInfo = seriesInfo

                }, cancellationToken);

                #endregion

                // Group series by name and year (if 2 series with the exact same name, the same year ...)
                var groupedResult = searchResultsTask.GroupBy(p => new { p.Name, p.ProductionYear },
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

            // Ensure that we don't create the same series multiple time 
            // We create series one at a time
            var seriesCreationLock = new Object();
            lock (seriesCreationLock)
            {
                series = GetMatchingSeries(request.NewSeriesName, request.NewSeriesYear, null);

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
                    _fileSystem.CreateDirectory(series.Path);

                    series.ProviderIds = request.NewSeriesProviderIds;
                }
            }

            // async outside of the lock for perfs
            var refreshOptions = new MetadataRefreshOptions(_fileSystem)
            {
                SearchResult = result
            };
            await series.RefreshMetadata(refreshOptions, cancellationToken).ConfigureAwait(false);

            return series;
        }

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
                    series = (Series)_libraryManager.GetItemById(new Guid(request.SeriesId));
                }

                await OrganizeEpisode(result.OriginalPath,
                    series,
                    request.SeasonNumber,
                    request.EpisodeNumber,
                    request.EndingEpisodeNumber,
                    null,
                    options,
                    request.RememberCorrection,
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

        private Task OrganizeEpisode(string sourcePath,
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
            var series = GetMatchingSeries(seriesName, seriesYear, result);

            if (series == null)
            {
                series = AutoDetectSeries(seriesName, null, options, cancellationToken).Result;

                if (series == null)
                {
                    var msg = string.Format("Unable to find series in library matching name {0}", seriesName);
                    result.Status = FileSortingStatus.Failure;
                    result.StatusMessage = msg;
                    _logger.Warn(msg);
                    return Task.FromResult(true);
                }
            }

            return OrganizeEpisode(sourcePath,
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
        /// Organize part responsible of Season AND Episode recognition
        /// </summary>
        /// <param name="sourcePath"></param>
        /// <param name="series"></param>
        /// <param name="seasonNumber"></param>
        /// <param name="episodeNumber"></param>
        /// <param name="endingEpiosdeNumber"></param>
        /// <param name="premiereDate"></param>
        /// <param name="options"></param>
        /// <param name="smartMatch"></param>
        /// <param name="rememberCorrection"></param>
        /// <param name="result"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        private async Task OrganizeEpisode(string sourcePath,
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
            var matchEpisode = await GetMatchingEpisode(series, seasonNumber, episodeNumber, endingEpiosdeNumber, premiereDate, cancellationToken);
            var episode = matchEpisode.Item1;

            Season season;
            season = !string.IsNullOrEmpty(episode.Season?.Path)
                ? episode.Season
                : await GetMatchingSeason(series, episode, options, cancellationToken);

            // Now we can check the episode Path
            if (string.IsNullOrEmpty(episode.Path))
            {
                SetEpisodeFileName(sourcePath, series, season, episode, options);
            }

            await OrganizeEpisode(sourcePath,
                series,
                episode,
                options,
                rememberCorrection,
                result,
                cancellationToken);
        }

        private async Task OrganizeEpisode(string sourcePath,
            Series series,
            Episode episode,
            TvFileOrganizationOptions options,
            bool rememberCorrection,
            FileOrganizationResult result,
            CancellationToken cancellationToken)
        {
            _logger.Info("Sorting file {0} into series {1}", sourcePath, series.Path);

            var originalExtractedSeriesString = result.ExtractedName;

            bool isNew = string.IsNullOrWhiteSpace(result.Id);

            if (isNew)
            {
                await _organizationService.SaveResult(result, cancellationToken);
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
                    var msg = string.Format("Unable to sort {0} because target path could not be determined.", sourcePath);
                    throw new OrganizationException(msg);
                }

                _logger.Info("Sorting file {0} to new path {1}", sourcePath, newPath);
                result.TargetPath = newPath;

                var fileExists = _fileSystem.FileExists(result.TargetPath);
                var otherDuplicatePaths = GetOtherDuplicatePaths(result.TargetPath, series, episode);

                if (!options.OverwriteExistingEpisodes)
                {
                    if (options.CopyOriginalFile && fileExists && IsSameEpisode(sourcePath, newPath))
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

                    if (otherDuplicatePaths.Count > 0)
                    {
                        var msg = string.Format("File '{0}' already exists as these:'{1}'. Stopping organization", sourcePath, string.Join("', '", otherDuplicatePaths));
                        _logger.Info(msg);
                        result.Status = FileSortingStatus.SkippedExisting;
                        result.StatusMessage = msg;
                        result.DuplicatePaths = otherDuplicatePaths;
                        return;
                    }
                }

                PerformFileSorting(options, result);

                if (options.OverwriteExistingEpisodes)
                {
                    var hasRenamedFiles = false;

                    foreach (var path in otherDuplicatePaths)
                    {
                        _logger.Debug("Removing duplicate episode {0}", path);

                        _libraryMonitor.ReportFileSystemChangeBeginning(path);

                        var renameRelatedFiles = !hasRenamedFiles &&
                            string.Equals(_fileSystem.GetDirectoryName(path), _fileSystem.GetDirectoryName(result.TargetPath), StringComparison.OrdinalIgnoreCase);

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
                            _logger.ErrorException("Error removing duplicate episode", ex, path);
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
                _logger.Warn(ex.Message);
                return;
            }
            finally
            {
                _organizationService.RemoveFromInprogressList(result);
            }

            if (rememberCorrection)
            {
                SaveSmartMatchString(originalExtractedSeriesString, series, cancellationToken);
            }
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
                    OrganizerType = FileOrganizerType.Episode,
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
            var directory = _fileSystem.GetDirectoryName(path);

            if (!string.IsNullOrWhiteSpace(originalFilenameWithoutExtension) && !string.IsNullOrWhiteSpace(directory))
            {
                // Get all related files, e.g. metadata, images, etc
                var files = _fileSystem.GetFilePaths(directory)
                    .Where(i => (Path.GetFileNameWithoutExtension(i) ?? string.Empty).StartsWith(originalFilenameWithoutExtension, StringComparison.OrdinalIgnoreCase))
                    .ToList();

                var targetFilenameWithoutExtension = Path.GetFileNameWithoutExtension(targetPath);

                foreach (var file in files)
                {
                    directory = _fileSystem.GetDirectoryName(file);
                    var filename = Path.GetFileName(file);

                    filename = filename.Replace(originalFilenameWithoutExtension, targetFilenameWithoutExtension,
                        StringComparison.OrdinalIgnoreCase);

                    var destination = Path.Combine(directory, filename);

                    _fileSystem.MoveFile(file, destination);
                }
            }
        }

        private List<string> GetOtherDuplicatePaths(string targetPath,
            Series series,
            Episode episode)
        {
            // TODO: Support date-naming?
            if (!series.ParentIndexNumber.HasValue || !episode.IndexNumber.HasValue)
            {
                return new List<string>();
            }

            var episodePaths = series.GetRecursiveChildren(i => i is Episode)
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

            var folder = _fileSystem.GetDirectoryName(targetPath);
            var targetFileNameWithoutExtension = _fileSystem.GetFileNameWithoutExtension(targetPath);

            try
            {
                var filesOfOtherExtensions = _fileSystem.GetFilePaths(folder)
                    .Where(i => _libraryManager.IsVideoFile(i) && string.Equals(_fileSystem.GetFileNameWithoutExtension(i), targetFileNameWithoutExtension, StringComparison.OrdinalIgnoreCase));

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

        private async Task<Tuple<Episode, RemoteSearchResult>> GetMatchingEpisode(Series series,
            int? seasonNumber,
            int? episodeNumber,
            int? endingEpiosdeNumber,
            DateTime? premiereDate,
            CancellationToken cancellationToken)
        {
            var episode = series
                .GetRecursiveChildren().OfType<Episode>()
                .FirstOrDefault(e => e.ParentIndexNumber == seasonNumber
                        && e.IndexNumber == episodeNumber
                        && e.IndexNumberEnd == endingEpiosdeNumber
                        && e.LocationType == LocationType.FileSystem);

            if (episode == null)
            {
                return await CreateNewEpisode(series, seasonNumber, episodeNumber, endingEpiosdeNumber, premiereDate, cancellationToken).ConfigureAwait(false);
            }

            return new Tuple<Episode, RemoteSearchResult>(episode, null);
        }

        private async Task<Season> GetMatchingSeason(Series series, Episode episode, TvFileOrganizationOptions options, CancellationToken cancellationToken)
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
                        var msg = string.Format("No season found for {0} season {1} episode {2}", series.Name,
                            episode.ParentIndexNumber, episode.IndexNumber);
                        _logger.Warn(msg);
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

            if (string.IsNullOrEmpty(season.Path))
            {
                season.Path = GetSeasonFolderPath(series, episode.ParentIndexNumber.Value, options);
                // Create the folder
                _fileSystem.CreateDirectory(season.Path);
            }

            return season;
        }

        private Series GetMatchingSeries(string seriesName, int? seriesYear, FileOrganizationResult result)
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
                .FirstOrDefault();

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

                    }).Cast<Series>().FirstOrDefault();
                }
            }

            return series;
        }

        /// <summary>
        /// Get the new series name
        /// </summary>
        /// <param name="series"></param>
        /// <param name="options"></param>
        /// <returns></returns>
        private string GetSeriesDirectoryName(Series series, TvFileOrganizationOptions options)
        {
            var seriesName = series.Name;
            var serieYear = series.ProductionYear;
            var seriesFullName = seriesName;
            if (series.ProductionYear.HasValue)
            {
                seriesFullName = string.Format("{0} ({1})", seriesFullName, series.ProductionYear);
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
        /// CreateNewEpisode
        /// </summary>
        /// <param name="series">The series.</param>
        /// <param name="seasonNumber">The season number.</param>
        /// <param name="episodeNumber">The episode number.</param>
        /// <param name="endingEpisodeNumber">The ending episode number.</param>
        /// <param name="premiereDate">The premiere date.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>System.String.</returns>
        private async Task<Tuple<Episode, RemoteSearchResult>> CreateNewEpisode(
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

            var searchResults = await _providerManager.GetRemoteSearchResults<Episode, EpisodeInfo>(new RemoteSearchQuery<EpisodeInfo>
            {
                SearchInfo = episodeInfo

            }, cancellationToken).ConfigureAwait(false);

            var episodeSearch = searchResults.FirstOrDefault();

            if (episodeSearch == null)
            {
                var msg = string.Format("No provider metadata found for {0} season {1} episode {2}", series.Name, seasonNumber, episodeNumber);
                _logger.Warn(msg);
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

            return new Tuple<Episode, RemoteSearchResult>(episode, episodeSearch);
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
