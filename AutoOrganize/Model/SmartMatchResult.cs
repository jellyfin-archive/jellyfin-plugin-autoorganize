using System;
using System.Collections.Generic;

namespace Emby.AutoOrganize.Model
{
    /// <summary>
    /// Used to customize matching of media files using a set of match strings.
    /// </summary>
    public class SmartMatchResult
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="SmartMatchResult"/> class.
        /// </summary>
        public SmartMatchResult()
        {
            Id = Guid.NewGuid();
            MatchStrings = new List<string>();
        }

        /// <summary>
        /// Gets or sets the unique identifier for this record.
        /// </summary>
        public Guid Id { get; set; }

        /// <summary>
        /// Gets or sets the item name. This should match the name of the target media item (movie, series, etc.)
        /// </summary>
        public string ItemName { get; set; }

        /// <summary>
        /// Gets or sets the display name of the smart match.
        /// </summary>
        /// <remarks>Currently, this is always the same as <see cref="ItemName"/>.</remarks>
        public string DisplayName { get; set; }

        /// <summary>
        /// Gets or sets the media type this smart match is meant to organize.
        /// </summary>
        public FileOrganizerType OrganizerType { get; set; }

        /// <summary>
        /// Gets the match strings used for auto-organizing.
        /// </summary>
        /// <remarks>
        /// When organizing, a media name (series, movie, etc.) is first extracted from the filepath. The extracted
        /// media name will be checked against this set of strings for a match.
        /// </remarks>
        public List<string> MatchStrings { get; }
    }
}
