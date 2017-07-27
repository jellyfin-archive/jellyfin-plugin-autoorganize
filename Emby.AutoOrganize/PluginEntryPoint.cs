using System;
using System.Threading;
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
using MediaBrowser.Model.Logging;
using MediaBrowser.Model.Tasks;

namespace Emby.AutoOrganize
{
    public class PluginEntryPoint : IServerEntryPoint
    {
        public static PluginEntryPoint Current;

        public IFileOrganizationService FileOrganizationService { get; private set; }
        private readonly ISessionManager _sessionManager;

        private readonly ITaskManager _taskManager;
        private readonly ILogger _logger;
        private readonly ILibraryMonitor _libraryMonitor;
        private readonly ILibraryManager _libraryManager;
        private readonly IServerConfigurationManager _config;
        private readonly IFileSystem _fileSystem;
        private readonly IProviderManager _providerManager;

        public IFileOrganizationRepository Repository;

        public PluginEntryPoint(ISessionManager sessionManager, ITaskManager taskManager, ILogger logger, ILibraryMonitor libraryMonitor, ILibraryManager libraryManager, IServerConfigurationManager config, IFileSystem fileSystem, IProviderManager providerManager)
        {
            _sessionManager = sessionManager;
            _taskManager = taskManager;
            _logger = logger;
            _libraryMonitor = libraryMonitor;
            _libraryManager = libraryManager;
            _config = config;
            _fileSystem = fileSystem;
            _providerManager = providerManager;
        }

        public void Run()
        {
            try
            {
                Repository = GetRepository();
            }
            catch (Exception ex)
            {
                _logger.ErrorException("Error initializing auto-organize database", ex);
            }

            Current = this;
            FileOrganizationService = new FileOrganizationService(_taskManager, Repository, _logger, _libraryMonitor, _libraryManager, _config, _fileSystem, _providerManager);

            FileOrganizationService.ItemAdded += _organizationService_ItemAdded;
            FileOrganizationService.ItemRemoved += _organizationService_ItemRemoved;
            FileOrganizationService.ItemUpdated += _organizationService_ItemUpdated;
            FileOrganizationService.LogReset += _organizationService_LogReset;
        }

        private IFileOrganizationRepository GetRepository()
        {
            var repo = new SqliteFileOrganizationRepository(_logger, _config.ApplicationPaths);

            repo.Initialize();

            return repo;
        }

        private void _organizationService_LogReset(object sender, EventArgs e)
        {
            _sessionManager.SendMessageToAdminSessions("AutoOrganize_LogReset", (FileOrganizationResult)null, CancellationToken.None);
        }

        private void _organizationService_ItemUpdated(object sender, GenericEventArgs<FileOrganizationResult> e)
        {
            _sessionManager.SendMessageToAdminSessions("AutoOrganize_ItemUpdated", e.Argument, CancellationToken.None);
        }

        private void _organizationService_ItemRemoved(object sender, GenericEventArgs<FileOrganizationResult> e)
        {
            _sessionManager.SendMessageToAdminSessions("AutoOrganize_ItemRemoved", e.Argument, CancellationToken.None);
        }

        private void _organizationService_ItemAdded(object sender, GenericEventArgs<FileOrganizationResult> e)
        {
            _sessionManager.SendMessageToAdminSessions("AutoOrganize_ItemAdded", e.Argument, CancellationToken.None);
        }

        public void Dispose()
        {
            FileOrganizationService.ItemAdded -= _organizationService_ItemAdded;
            FileOrganizationService.ItemRemoved -= _organizationService_ItemRemoved;
            FileOrganizationService.ItemUpdated -= _organizationService_ItemUpdated;
            FileOrganizationService.LogReset -= _organizationService_LogReset;

            var repo = Repository as IDisposable;
            if (repo != null)
            {
                repo.Dispose();
            }
        }
    }
}
