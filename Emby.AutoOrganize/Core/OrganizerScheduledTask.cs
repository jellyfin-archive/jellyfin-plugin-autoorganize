using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Tasks;
using Emby.AutoOrganize.Model;
using MediaBrowser.Controller.Configuration;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.IO;
using MediaBrowser.Model.Tasks;
using Microsoft.Extensions.Logging;

namespace Emby.AutoOrganize.Core
{
    /// <summary>
    /// A scheduled task that organizes media files.
    /// </summary>
    public class OrganizerScheduledTask : IScheduledTask, IConfigurableScheduledTask
    {
        private readonly ILibraryMonitor _libraryMonitor;
        private readonly ILibraryManager _libraryManager;
        private readonly ILogger _logger;
        private readonly IFileSystem _fileSystem;
        private readonly IServerConfigurationManager _config;
        private readonly IProviderManager _providerManager;

        /// <summary>
        /// Initializes a new instance of the <see cref="OrganizerScheduledTask"/> class.
        /// </summary>
        [SuppressMessage("StyleCop.CSharp.DocumentationRules", "SA1611:Element parameters should be documented", Justification = "Parameter types/names are self-documenting")]
        public OrganizerScheduledTask(ILibraryMonitor libraryMonitor, ILibraryManager libraryManager, ILogger logger, IFileSystem fileSystem, IServerConfigurationManager config, IProviderManager providerManager)
        {
            _libraryMonitor = libraryMonitor;
            _libraryManager = libraryManager;
            _logger = logger;
            _fileSystem = fileSystem;
            _config = config;
            _providerManager = providerManager;
        }

        /// <inheritdoc/>
        public string Key => "AutoOrganize";

        /// <inheritdoc/>
        public string Name => "Organize new media files";

        /// <inheritdoc/>
        public string Description => "Processes new files available in the configured watch folder.";

        /// <inheritdoc/>
        public string Category => "Library";

        /// <inheritdoc/>
        public bool IsHidden =>
            !_config.GetAutoOrganizeOptions().TvOptions.IsEnabled
            && !_config.GetAutoOrganizeOptions().MovieOptions.IsEnabled;

        /// <inheritdoc/>
        public bool IsEnabled =>
            _config.GetAutoOrganizeOptions().TvOptions.IsEnabled
            || _config.GetAutoOrganizeOptions().MovieOptions.IsEnabled;

        /// <inheritdoc/>
        public bool IsLogged => false;

        /// <inheritdoc/>
        public async Task Execute(CancellationToken cancellationToken, IProgress<double> progress)
        {
            bool queueTv = false, queueMovie = false;

            var options = _config.GetAutoOrganizeOptions();

            if (options.TvOptions.IsEnabled)
            {
                queueTv = options.TvOptions.QueueLibraryScan;
                var fileOrganizationService = PluginEntryPoint.Current.FileOrganizationService;

                await new TvFolderOrganizer(_libraryManager, _logger, _fileSystem, _libraryMonitor, fileOrganizationService, _config, _providerManager)
                    .Organize(options.TvOptions, progress, cancellationToken).ConfigureAwait(false);
            }

            if (options.MovieOptions.IsEnabled)
            {
                queueMovie = options.MovieOptions.QueueLibraryScan;
                var fileOrganizationService = PluginEntryPoint.Current.FileOrganizationService;

                await new MovieFolderOrganizer(_libraryManager, _logger, _fileSystem, _libraryMonitor, fileOrganizationService, _config, _providerManager)
                    .Organize(options.MovieOptions, progress, cancellationToken).ConfigureAwait(false);
            }

            if ((queueTv || queueMovie) && !_libraryManager.IsScanRunning)
            {
                _libraryManager.QueueLibraryScan();
            }
        }

        /// <inheritdoc/>
        public IEnumerable<TaskTriggerInfo> GetDefaultTriggers()
        {
            return new[]
            {
                // Every so often
                new TaskTriggerInfo { Type = TaskTriggerInfo.TriggerInterval, IntervalTicks = TimeSpan.FromMinutes(5).Ticks }
            };
        }
    }
}
