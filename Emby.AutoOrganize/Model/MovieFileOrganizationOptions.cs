using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace Emby.AutoOrganize.Model
{
    /// <summary>
    /// Options to use for organizing movie media files.
    /// </summary>
    public class MovieFileOrganizationOptions
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="MovieFileOrganizationOptions"/> class.
        /// </summary>
        public MovieFileOrganizationOptions()
        {
            MinFileSizeMb = 50;
            LeftOverFileExtensionsToDelete = new List<string>();
            MoviePattern = "%fn.%ext";
            WatchLocations = new List<string>();
            CopyOriginalFile = false;
            MovieFolder = false;
            MovieFolderPattern = "%mn (%my)";
            QueueLibraryScan = false;
            ExtendedClean = false;
        }

        /// <summary>
        /// Gets or sets a value indicating whether movie organization is enabled.
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
        /// Gets or sets the pattern to use for generating movie file names when moving them to the target location.
        /// </summary>
        public string MoviePattern { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether existing media files should be overwritten during organization.
        /// </summary>
        public bool OverwriteExistingFiles { get; set; }

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
        /// Gets or sets a value indicating whether auto-detection should be enabled during organization for movies not
        /// already in the media library.
        /// </summary>
        public bool AutoDetectMovie { get; set; }

        /// <summary>
        /// Gets or sets the default movie library path to use for new movies when auto-detection is turned on.
        /// </summary>
        public string DefaultMovieLibraryPath { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether a separate sub-directory should be created for each movie when
        /// organizing.
        /// </summary>
        public bool MovieFolder { get; set; }

        /// <summary>
        /// Gets or sets the pattern to use for generating movie folder names when <see cref="MovieFolder"/> is set to
        /// true.
        /// </summary>
        public string MovieFolderPattern { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether a library scan should be queued after an organization operation completes.
        /// </summary>
        public bool QueueLibraryScan { get; set; }
    }
}
