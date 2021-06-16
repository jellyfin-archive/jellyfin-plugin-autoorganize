using System.Collections.Generic;

namespace Emby.AutoOrganize.Model
{
    /// <summary>
    /// A request to organize a single media file representing one or more TV episodes.
    /// </summary>
    public class EpisodeFileOrganizationRequest
    {
        /// <summary>
        /// Gets or sets the existing organization result that failed. This is required when organizing a file with a
        /// user-supplied correction.
        /// </summary>
        public string ResultId { get; set; }

        /// <summary>
        /// Gets or sets the ID of the series that contains the episode.
        /// </summary>
        public string SeriesId { get; set; }

        /// <summary>
        /// Gets or sets the season number the episode is in.
        /// </summary>
        public int SeasonNumber { get; set; }

        /// <summary>
        /// Gets or sets the episode number.
        /// </summary>
        public int EpisodeNumber { get; set; }

        /// <summary>
        /// Gets or sets the ending episode number.
        /// </summary>
        public int? EndingEpisodeNumber { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether a new smart match entry should be created as a result of this request.
        /// </summary>
        public bool RememberCorrection { get; set; }

        /// <summary>
        /// Gets or sets the series name. Only required if this episode is part of a series that has not yet been added
        /// to the library, otherwise this can be null.
        /// </summary>
        public string NewSeriesName { get; set; }

        /// <summary>
        /// Gets or sets the series year. Only required if this episode is part of a series that has not yet been added
        /// to the library, otherwise this can be null.
        /// </summary>
        public int? NewSeriesYear { get; set; }

        /// <summary>
        /// Gets or sets the base target folder of the media library.
        /// </summary>
        public string TargetFolder { get; set; }

        /// <summary>
        /// Gets or sets the provider IDs for the series. Only required if this episode is part of a series that has
        /// not yet been added to the library, otherwise this can be empty.
        /// </summary>
        public IReadOnlyDictionary<string, string> NewSeriesProviderIds { get; set; }
    }
}
