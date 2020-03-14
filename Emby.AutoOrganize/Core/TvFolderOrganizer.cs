using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Emby.AutoOrganize.Model;
using MediaBrowser.Controller.Configuration;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.IO;
using Microsoft.Extensions.Logging;

namespace Emby.AutoOrganize.Core
{
    /// <summary>
    /// Service used to organize all files in the TV watch folders.
    /// </summary>
    public class TvFolderOrganizer
    {
        private readonly ILibraryMonitor _libraryMonitor;
        private readonly ILibraryManager _libraryManager;
        private readonly ILogger _logger;
        private readonly IFileSystem _fileSystem;
        private readonly IFileOrganizationService _organizationService;
        private readonly IServerConfigurationManager _config;
        private readonly IProviderManager _providerManager;

        /// <summary>
        /// Initializes a new instance of the <see cref="TvFolderOrganizer"/> class.
        /// </summary>
        [SuppressMessage("StyleCop.CSharp.DocumentationRules", "SA1611:Element parameters should be documented", Justification = "Parameter types/names are self-documenting")]
        public TvFolderOrganizer(ILibraryManager libraryManager, ILogger logger, IFileSystem fileSystem, ILibraryMonitor libraryMonitor, IFileOrganizationService organizationService, IServerConfigurationManager config, IProviderManager providerManager)
        {
            _libraryManager = libraryManager;
            _logger = logger;
            _fileSystem = fileSystem;
            _libraryMonitor = libraryMonitor;
            _organizationService = organizationService;
            _config = config;
            _providerManager = providerManager;
        }

        private bool EnableOrganization(FileSystemMetadata fileInfo, TvFileOrganizationOptions options)
        {
            var minFileBytes = options.MinFileSizeMb * 1024 * 1024;

            try
            {
                return _libraryManager.IsVideoFile(fileInfo.FullName) && fileInfo.Length >= minFileBytes;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error organizing file {0}", ex, fileInfo.Name);
            }

            return false;
        }

        private bool IsValidWatchLocation(string path, List<string> libraryFolderPaths)
        {
            if (IsPathAlreadyInMediaLibrary(path, libraryFolderPaths))
            {
                _logger.LogInformation("Folder {0} is not eligible for auto-organize because it is also part of an Emby library", path);
                return false;
            }

            return true;
        }

        private bool IsPathAlreadyInMediaLibrary(string path, List<string> libraryFolderPaths)
        {
            return libraryFolderPaths.Any(i => string.Equals(i, path, StringComparison.Ordinal) || _fileSystem.ContainsSubPath(i, path));
        }

        /// <summary>
        /// Perform organization for the TV watch folders.
        /// </summary>
        /// <param name="options">The organization options.</param>
        /// <param name="progress">The <see cref="IProgress{T}"/> to use for reporting operation progress.</param>
        /// <param name="cancellationToken">A cancellation token for the operation.</param>
        /// <returns>A task representing the operation completion.</returns>
        public async Task Organize(
            TvFileOrganizationOptions options,
            IProgress<double> progress,
            CancellationToken cancellationToken)
        {
            var libraryFolderPaths = _libraryManager.GetVirtualFolders().SelectMany(i => i.Locations).ToList();

            var watchLocations = options.WatchLocations
                .Where(i => IsValidWatchLocation(i, libraryFolderPaths))
                .ToList();

            var eligibleFiles = watchLocations.SelectMany(GetFilesToOrganize)
                .OrderBy(_fileSystem.GetCreationTimeUtc)
                .Where(i => EnableOrganization(i, options))
                .ToList();

            var processedFolders = new HashSet<string>();

            progress.Report(10);

            if (eligibleFiles.Count > 0)
            {
                var numComplete = 0;

                var organizer = new EpisodeFileOrganizer(_organizationService, _fileSystem, _logger, _libraryManager, _libraryMonitor, _providerManager);

                foreach (var file in eligibleFiles)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    try
                    {
                        var result = await organizer.OrganizeEpisodeFile(file.FullName, options, cancellationToken).ConfigureAwait(false);

                        if (result.Status == FileSortingStatus.Success && !processedFolders.Contains(file.DirectoryName, StringComparer.OrdinalIgnoreCase))
                        {
                            processedFolders.Add(file.DirectoryName);
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error organizing episode {0}", file.FullName);
                    }

                    numComplete++;
                    double percent = numComplete;
                    percent /= eligibleFiles.Count;

                    progress.Report(10 + (89 * percent));
                }
            }

            cancellationToken.ThrowIfCancellationRequested();
            progress.Report(99);

            var deleteExtensions = options.LeftOverFileExtensionsToDelete
                .Select(i => i.Trim().TrimStart('.'))
                .Where(i => !string.IsNullOrEmpty(i))
                .Select(i => "." + i)
                .ToList();

            // Normal Clean
            Clean(processedFolders, watchLocations, options.DeleteEmptyFolders, deleteExtensions);

            // Extended Clean
            if (options.ExtendedClean)
            {
                Clean(watchLocations, watchLocations, options.DeleteEmptyFolders, deleteExtensions);
            }

            progress.Report(100);
        }

        private void Clean(IEnumerable<string> paths, List<string> watchLocations, bool deleteEmptyFolders, List<string> deleteExtensions)
        {
            foreach (var path in paths)
            {
                if (deleteExtensions.Count > 0)
                {
                    DeleteLeftOverFiles(path, deleteExtensions);
                }

                if (deleteEmptyFolders)
                {
                    DeleteEmptyFolders(path, watchLocations);
                }
            }
        }

        /// <summary>
        /// Gets the files to organize.
        /// </summary>
        /// <param name="path">The path.</param>
        /// <returns>IEnumerable{FileInfo}.</returns>
        private List<FileSystemMetadata> GetFilesToOrganize(string path)
        {
            try
            {
                return _fileSystem.GetFiles(path, true)
                    .ToList();
            }
            catch (IOException ex)
            {
                _logger.LogError(ex, "Error getting files from {0}", path);

                return new List<FileSystemMetadata>();
            }
        }

        /// <summary>
        /// Deletes the left over files.
        /// </summary>
        /// <param name="path">The path.</param>
        /// <param name="extensions">The extensions.</param>
        private void DeleteLeftOverFiles(string path, IEnumerable<string> extensions)
        {
            var eligibleFiles = _fileSystem.GetFilePaths(path, extensions.ToArray(), false, true)
                .ToList();

            foreach (var file in eligibleFiles)
            {
                try
                {
                    _fileSystem.DeleteFile(file);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error deleting file {0}", file);
                }
            }
        }

        /// <summary>
        /// Deletes the empty folders.
        /// </summary>
        /// <param name="path">The path.</param>
        /// <param name="ignorePaths">A set of paths to ignore and not delete.</param>
        private void DeleteEmptyFolders(string path, List<string> ignorePaths)
        {
            try
            {
                foreach (var d in _fileSystem.GetDirectoryPaths(path))
                {
                    DeleteEmptyFolders(d, ignorePaths);
                }

                var entries = _fileSystem.GetFileSystemEntryPaths(path);

                if (!entries.Any() && !IsWatchFolder(path, ignorePaths))
                {
                    _logger.LogDebug("Deleting empty directory {0}", path);
                    Directory.Delete(path, false);
                }
            }
            catch (Exception ex) when (ex is UnauthorizedAccessException || ex is IOException)
            {
                _logger.LogError("Failed to delete empty TV directory");
            }
        }

        /// <summary>
        /// Determines if a given folder path is contained in a folder list.
        /// </summary>
        /// <param name="path">The folder path to check.</param>
        /// <param name="watchLocations">A list of folders.</param>
        private bool IsWatchFolder(string path, IEnumerable<string> watchLocations)
        {
            return watchLocations.Contains(path, StringComparer.OrdinalIgnoreCase);
        }
    }
}
