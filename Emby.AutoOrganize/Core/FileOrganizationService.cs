using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Emby.AutoOrganize.Data;
using Emby.AutoOrganize.Model;
using MediaBrowser.Common.Events;
using MediaBrowser.Common.Extensions;
using MediaBrowser.Controller.Configuration;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Persistence;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Events;
using MediaBrowser.Model.IO;
using MediaBrowser.Model.Logging;
using MediaBrowser.Model.Querying;
using MediaBrowser.Model.Tasks;

namespace Emby.AutoOrganize.Core
{
    public class FileOrganizationService : IFileOrganizationService
    {
        private readonly ITaskManager _taskManager;
        private readonly IFileOrganizationRepository _repo;
        private readonly ILogger _logger;
        private readonly ILibraryMonitor _libraryMonitor;
        private readonly ILibraryManager _libraryManager;
        private readonly IServerConfigurationManager _config;
        private readonly IFileSystem _fileSystem;
        private readonly IProviderManager _providerManager;
        private readonly ConcurrentDictionary<string, bool> _inProgressItemIds = new ConcurrentDictionary<string, bool>();

        public event EventHandler<GenericEventArgs<FileOrganizationResult>> ItemAdded;
        public event EventHandler<GenericEventArgs<FileOrganizationResult>> ItemUpdated;
        public event EventHandler<GenericEventArgs<FileOrganizationResult>> ItemRemoved;
        public event EventHandler LogReset;

        public FileOrganizationService(ITaskManager taskManager, IFileOrganizationRepository repo, ILogger logger, ILibraryMonitor libraryMonitor, ILibraryManager libraryManager, IServerConfigurationManager config, IFileSystem fileSystem, IProviderManager providerManager)
        {
            _taskManager = taskManager;
            _repo = repo;
            _logger = logger;
            _libraryMonitor = libraryMonitor;
            _libraryManager = libraryManager;
            _config = config;
            _fileSystem = fileSystem;
            _providerManager = providerManager;
        }

        public void BeginProcessNewFiles()
        {
            _taskManager.CancelIfRunningAndQueue<OrganizerScheduledTask>();
        }

        public void SaveResult(FileOrganizationResult result, CancellationToken cancellationToken)
        {
            if (result == null || string.IsNullOrEmpty(result.OriginalPath))
            {
                throw new ArgumentNullException("result");
            }

            result.Id = result.OriginalPath.GetMD5().ToString("N");

            _repo.SaveResult(result, cancellationToken);
        }

        public void SaveResult(SmartMatchResult result, CancellationToken cancellationToken)
        {
            if (result == null)
            {
                throw new ArgumentNullException("result");
            }

            _repo.SaveResult(result, cancellationToken);
        }

        public QueryResult<FileOrganizationResult> GetResults(FileOrganizationResultQuery query)
        {
            var results = _repo.GetResults(query);

            foreach (var result in results.Items)
            {
                result.IsInProgress = _inProgressItemIds.ContainsKey(result.Id);
            }

            return results;
        }

        public FileOrganizationResult GetResult(string id)
        {
            var result = _repo.GetResult(id);

            if (result != null)
            {
                result.IsInProgress = _inProgressItemIds.ContainsKey(result.Id);
            }

            return result;
        }

        public FileOrganizationResult GetResultBySourcePath(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                throw new ArgumentNullException("path");
            }

            var id = path.GetMD5().ToString("N");

            return GetResult(id);
        }

        public async Task DeleteOriginalFile(string resultId)
        {
            var result = _repo.GetResult(resultId);

            _logger.Info("Requested to delete {0}", result.OriginalPath);

            if (!AddToInProgressList(result, false))
            {
                throw new OrganizationException("Path is currently processed otherwise. Please try again later.");
            }

            try
            {
                _fileSystem.DeleteFile(result.OriginalPath);
            }
            catch (Exception ex)
            {
                _logger.ErrorException("Error deleting {0}", ex, result.OriginalPath);
            }
            finally
            {
                RemoveFromInprogressList(result);
            }

            await _repo.Delete(resultId);

            EventHelper.FireEventIfNotNull(ItemRemoved, this, new GenericEventArgs<FileOrganizationResult>(result), _logger);
        }

        private AutoOrganizeOptions GetAutoOrganizeOptions()
        {
            return _config.GetAutoOrganizeOptions();
        }

        public async Task PerformOrganization(string resultId)
        {
            var result = _repo.GetResult(resultId);

            var options = GetAutoOrganizeOptions();

            if (string.IsNullOrEmpty(result.TargetPath))
            {
                throw new ArgumentException("No target path available.");
            }

            FileOrganizationResult organizeResult;
            switch (result.Type)
            {
                case FileOrganizerType.Episode:
                    var episodeOrganizer = new EpisodeFileOrganizer(this, _config, _fileSystem, _logger, _libraryManager,
                        _libraryMonitor, _providerManager);

                    organizeResult = await episodeOrganizer.OrganizeEpisodeFile(result.OriginalPath, options.TvOptions, CancellationToken.None)
                        .ConfigureAwait(false);

                    break;
                case FileOrganizerType.Movie:
                    var movieOrganizer = new MovieFileOrganizer(this, _config, _fileSystem, _logger, _libraryManager,
                        _libraryMonitor, _providerManager);

                    organizeResult = await movieOrganizer.OrganizeMovieFile(result.OriginalPath, options.MovieOptions, true, CancellationToken.None)
                        .ConfigureAwait(false);
                    break;
                default:
                    throw new OrganizationException("No organizer exist for the type " + result.Type);
            }

            if (organizeResult.Status != FileSortingStatus.Success)
            {
                throw new OrganizationException(result.StatusMessage);
            }
        }

        public async Task ClearLog()
        {
            await _repo.DeleteAll();
            EventHelper.FireEventIfNotNull(LogReset, this, EventArgs.Empty, _logger);
        }

        public async Task ClearCompleted()
        {
            await _repo.DeleteCompleted();
            EventHelper.FireEventIfNotNull(LogReset, this, EventArgs.Empty, _logger);
        }

        public async Task PerformOrganization(EpisodeFileOrganizationRequest request)
        {
            var organizer = new EpisodeFileOrganizer(this, _config, _fileSystem, _logger, _libraryManager,
                _libraryMonitor, _providerManager);

            var options = GetAutoOrganizeOptions();
            var result = await organizer.OrganizeWithCorrection(request, options.TvOptions, CancellationToken.None).ConfigureAwait(false);

            if (result.Status != FileSortingStatus.Success)
            {
                throw new Exception(result.StatusMessage);
            }
        }

        public async Task PerformOrganization(MovieFileOrganizationRequest request)
        {
            var organizer = new MovieFileOrganizer(this, _config, _fileSystem, _logger, _libraryManager,
                _libraryMonitor, _providerManager);

            var options = GetAutoOrganizeOptions();
            var result = await organizer.OrganizeWithCorrection(request, options.MovieOptions, CancellationToken.None).ConfigureAwait(false);

            if (result.Status != FileSortingStatus.Success)
            {
                throw new Exception(result.StatusMessage);
            }
        }

        public QueryResult<SmartMatchResult> GetSmartMatchInfos(FileOrganizationResultQuery query)
        {
            return _repo.GetSmartMatch(query);
        }


        public QueryResult<SmartMatchResult> GetSmartMatchInfos()
        {
            return _repo.GetSmartMatch(new FileOrganizationResultQuery());
        }

        public void DeleteSmartMatchEntry(string id, string matchString)
        {
            if (string.IsNullOrEmpty(id))
            {
                throw new ArgumentNullException(nameof(id));
            }

            if (string.IsNullOrEmpty(matchString))
            {
                throw new ArgumentNullException(nameof(matchString));
            }

            _repo.DeleteSmartMatch(id, matchString);
        }

        /// <summary>
        /// Attempts to add a an item to the list of currently processed items.
        /// </summary>
        /// <param name="result">The result item.</param>
        /// <param name="isNewItem">Passing true will notify the client to reload all items, otherwise only a single item will be refreshed.</param>
        /// <returns>True if the item was added, False if the item is already contained in the list.</returns>
        public bool AddToInProgressList(FileOrganizationResult result, bool isNewItem)
        {
            if (string.IsNullOrWhiteSpace(result.Id))
            {
                result.Id = result.OriginalPath.GetMD5().ToString("N");
            }

            if (!_inProgressItemIds.TryAdd(result.Id, false))
            {
                return false;
            }

            result.IsInProgress = true;

            if (isNewItem)
            {
                EventHelper.FireEventIfNotNull(ItemAdded, this, new GenericEventArgs<FileOrganizationResult>(result), _logger);
            }
            else
            {
                EventHelper.FireEventIfNotNull(ItemUpdated, this, new GenericEventArgs<FileOrganizationResult>(result), _logger);
            }

            return true;
        }

        /// <summary>
        /// Removes an item from the list of currently processed items.
        /// </summary>
        /// <param name="result">The result item.</param>
        /// <returns>True if the item was removed, False if the item was not contained in the list.</returns>
        public bool RemoveFromInprogressList(FileOrganizationResult result)
        {
            bool itemValue;
            var retval = _inProgressItemIds.TryRemove(result.Id, out itemValue);

            result.IsInProgress = false;

            EventHelper.FireEventIfNotNull(ItemUpdated, this, new GenericEventArgs<FileOrganizationResult>(result), _logger);

            return retval;
        }

    }
}
