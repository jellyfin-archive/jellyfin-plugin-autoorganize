using System;
using System.Threading;
using System.Threading.Tasks;
using Emby.AutoOrganize.Core;
using Emby.AutoOrganize.Data;
using Emby.AutoOrganize.Model;
using MediaBrowser.Controller.Configuration;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Plugins;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Controller.Session;
using MediaBrowser.Model.Events;
using MediaBrowser.Model.IO;
using MediaBrowser.Model.Serialization;
using MediaBrowser.Model.Tasks;
using Microsoft.Extensions.Logging;

namespace Emby.AutoOrganize
{
    public sealed class PluginEntryPoint : IServerEntryPoint
    {
        private readonly ISessionManager _sessionManager;
        private readonly ITaskManager _taskManager;
        private readonly ILogger _logger;
        private readonly ILibraryMonitor _libraryMonitor;
        private readonly ILibraryManager _libraryManager;
        private readonly IServerConfigurationManager _config;
        private readonly IFileSystem _fileSystem;
        private readonly IProviderManager _providerManager;
        private readonly IJsonSerializer _json;

        private IFileOrganizationRepository _repository;

        public PluginEntryPoint(
            ISessionManager sessionManager,
            ITaskManager taskManager,
            ILoggerFactory loggerFactory,
            ILibraryMonitor libraryMonitor,
            ILibraryManager libraryManager,
            IServerConfigurationManager config,
            IFileSystem fileSystem,
            IProviderManager providerManager,
            IJsonSerializer json)
        {
            _sessionManager = sessionManager;
            _taskManager = taskManager;
            _logger = loggerFactory.CreateLogger("AutoOrganize");
            _libraryMonitor = libraryMonitor;
            _libraryManager = libraryManager;
            _config = config;
            _fileSystem = fileSystem;
            _providerManager = providerManager;
            _json = json;
        }

        public static PluginEntryPoint Current { get; private set; }

        public IFileOrganizationService FileOrganizationService { get; private set; }

        /// <inheritdoc/>
        public Task RunAsync()
        {
            try
            {
                _repository = GetRepository();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error initializing auto-organize database");
            }

            Current = this;
            FileOrganizationService = new FileOrganizationService(_taskManager, _repository, _logger, _libraryMonitor, _libraryManager, _config, _fileSystem, _providerManager);

            FileOrganizationService.ItemAdded += OrganizationService_ItemAdded;
            FileOrganizationService.ItemRemoved += OrganizationService_ItemRemoved;
            FileOrganizationService.ItemUpdated += OrganizationService_ItemUpdated;
            FileOrganizationService.LogReset += OrganizationService_LogReset;

            // Convert Config
            _config.Convert(FileOrganizationService);

            return Task.CompletedTask;
        }

        private IFileOrganizationRepository GetRepository()
        {
            var repo = new SqliteFileOrganizationRepository(_logger, _config.ApplicationPaths, _json);

            repo.Initialize();

            return repo;
        }

        private void OrganizationService_LogReset(object sender, EventArgs e)
        {
            _sessionManager.SendMessageToAdminSessions("AutoOrganize_LogReset", (FileOrganizationResult)null, CancellationToken.None);
        }

        private void OrganizationService_ItemUpdated(object sender, GenericEventArgs<FileOrganizationResult> e)
        {
            _sessionManager.SendMessageToAdminSessions("AutoOrganize_ItemUpdated", e.Argument, CancellationToken.None);
        }

        private void OrganizationService_ItemRemoved(object sender, GenericEventArgs<FileOrganizationResult> e)
        {
            _sessionManager.SendMessageToAdminSessions("AutoOrganize_ItemRemoved", e.Argument, CancellationToken.None);
        }

        private void OrganizationService_ItemAdded(object sender, GenericEventArgs<FileOrganizationResult> e)
        {
            _sessionManager.SendMessageToAdminSessions("AutoOrganize_ItemAdded", e.Argument, CancellationToken.None);
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            FileOrganizationService.ItemAdded -= OrganizationService_ItemAdded;
            FileOrganizationService.ItemRemoved -= OrganizationService_ItemRemoved;
            FileOrganizationService.ItemUpdated -= OrganizationService_ItemUpdated;
            FileOrganizationService.LogReset -= OrganizationService_LogReset;

            var repo = _repository as IDisposable;
            if (repo != null)
            {
                repo.Dispose();
            }
        }
    }
}
