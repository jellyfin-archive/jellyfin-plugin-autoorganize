using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace Emby.AutoOrganize.Model
{
    /// <summary>
    /// Options to use for organizing TV media files.
    /// </summary>
    public class TvFileOrganizationOptions
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="TvFileOrganizationOptions"/> class.
        /// </summary>
        public TvFileOrganizationOptions()
        {
            MinFileSizeMb = 50;
            LeftOverFileExtensionsToDelete = new List<string>();
            WatchLocations = new List<string>();
            EpisodeNamePattern = "%sn - %sx%0e - %en.%ext";
            MultiEpisodeNamePattern = "%sn - %sx%0e-x%0ed - %en.%ext";
            SeasonFolderPattern = "Season %s";
            SeasonZeroFolderName = "Season 0";
            SeriesFolderPattern = "%fn";
            CopyOriginalFile = false;
            QueueLibraryScan = false;
            ExtendedClean = false;
        }

        /// <summary>
        /// Gets or sets a value indicating whether TV organization is enabled.
        /// </summary>
        public bool IsEnabled { get; set; }

        /// <summary>
        /// Gets or sets the minimum required file size (in MB) before a file is considered for organization.
        /// </summary>
        public int MinFileSizeMb { get; set; }

        /// <summary>
        /// Gets or sets the list of file extensions that should be deleted after an organization operation.
        /// </summary>
        [SuppressMessage("Usage", "CA2227:Collection properties should be read only", Justification = "This property needs to support serialization by both ServiceStack and XmlSerializer")]
        public List<string> LeftOverFileExtensionsToDelete { get; set; }

        /// <summary>
        /// Gets or sets the list of folders to watch for files to organize.
        /// </summary>
        [SuppressMessage("Usage", "CA2227:Collection properties should be read only", Justification = "This property needs to support serialization by both ServiceStack and XmlSerializer")]
        public List<string> WatchLocations { get; set; }

        /// <summary>
        /// Gets or sets the patter to use for generating season folder names.
        /// </summary>
        public string SeasonFolderPattern { get; set; }

        /// <summary>
        /// Gets or sets the name to use for the specials folder.
        /// </summary>
        public string SeasonZeroFolderName { get; set; }

        /// <summary>
        /// Gets or sets the pattern to use for generating episode file names.
        /// </summary>
        public string EpisodeNamePattern { get; set; }

        /// <summary>
        /// Gets or sets the pattern to use for generating filenames for multi-episode files.
        /// </summary>
        public string MultiEpisodeNamePattern { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether existing media files should be overwritten during organization.
        /// </summary>
        public bool OverwriteExistingEpisodes { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether empty folders should be deleted after an organization operation completes.
        /// </summary>
        public bool DeleteEmptyFolders { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether an extended clean should be performed after an organization
        /// operation completes. During an extended clean, the entire set of watch folders is cleaned instead of only
        /// the folders that were processed.
        /// </summary>
        public bool ExtendedClean { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether original files should be copied during organization. If false, they
        /// will be moved instead.
        /// </summary>
        public bool CopyOriginalFile { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether auto-detection should be enabled during organization for series not
        /// already in the media library.
        /// </summary>
        public bool AutoDetectSeries { get; set; }

        /// <summary>
        /// Gets or sets the default TV series library path to use for new series when auto-detection is turned on.
        /// </summary>
        public string DefaultSeriesLibraryPath { get; set; }

        /// <summary>
        /// Gets or sets the pattern to use for generating series folder names when <see cref="AutoDetectSeries"/> is
        /// set to true.
        /// </summary>
        public string SeriesFolderPattern { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether a library scan should be queued after an organization operation completes.
        /// </summary>
        public bool QueueLibraryScan { get; set; }
    }
}
