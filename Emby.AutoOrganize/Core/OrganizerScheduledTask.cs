using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Emby.AutoOrganize.Model;
using MediaBrowser.Controller.Configuration;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.IO;
using MediaBrowser.Model.Logging;
using MediaBrowser.Model.Tasks;

namespace Emby.AutoOrganize.Core
{
    public class OrganizerScheduledTask : IScheduledTask, IConfigurableScheduledTask
    {
        private readonly ILibraryMonitor _libraryMonitor;
        private readonly ILibraryManager _libraryManager;
        private readonly ILogger _logger;
        private readonly IFileSystem _fileSystem;
        private readonly IServerConfigurationManager _config;
        private readonly IProviderManager _providerManager;

        public OrganizerScheduledTask(ILibraryMonitor libraryMonitor, ILibraryManager libraryManager, ILogger logger, IFileSystem fileSystem, IServerConfigurationManager config, IProviderManager providerManager)
        {
            _libraryMonitor = libraryMonitor;
            _libraryManager = libraryManager;
            _logger = logger;
            _fileSystem = fileSystem;
            _config = config;
            _providerManager = providerManager;
        }

        public string Name
        {
            get { return "Organize new media files"; }
        }

        public string Description
        {
            get { return "Processes new files available in the configured watch folder."; }
        }

        public string Category
        {
            get { return "Library"; }
        }

        private AutoOrganizeOptions GetAutoOrganizeOptions()
        {
            return _config.GetAutoOrganizeOptions();
        }

        public async Task Execute(CancellationToken cancellationToken, IProgress<double> progress)
        {
            if (GetAutoOrganizeOptions().TvOptions.IsEnabled)
            {
                var fileOrganizationService = PluginEntryPoint.Current.FileOrganizationService;

                await new TvFolderOrganizer(_libraryManager, _logger, _fileSystem, _libraryMonitor, fileOrganizationService, _config, _providerManager)
                    .Organize(GetAutoOrganizeOptions(), cancellationToken, progress).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Creates the triggers that define when the task will run
        /// </summary>
        /// <returns>IEnumerable{BaseTaskTrigger}.</returns>
        public IEnumerable<TaskTriggerInfo> GetDefaultTriggers()
        {
            return new[] { 
            
                // Every so often
                new TaskTriggerInfo { Type = TaskTriggerInfo.TriggerInterval, IntervalTicks = TimeSpan.FromMinutes(5).Ticks}
            };
        }

        public bool IsHidden
        {
            get { return !GetAutoOrganizeOptions().TvOptions.IsEnabled; }
        }

        public bool IsEnabled
        {
            get { return GetAutoOrganizeOptions().TvOptions.IsEnabled; }
        }

        public bool IsLogged
        {
            get { return false; }
        }

        public string Key
        {
            get { return "AutoOrganize"; }
        }
    }
}
