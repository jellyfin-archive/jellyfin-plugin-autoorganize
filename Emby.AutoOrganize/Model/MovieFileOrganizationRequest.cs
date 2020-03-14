using System.Collections.Generic;

namespace Emby.AutoOrganize.Model
{
    /// <summary>
    /// A request to organize a single movie media file.
    /// </summary>
    public class MovieFileOrganizationRequest
    {
        /// <summary>
        /// Gets or sets the existing organization result that failed. This is required when organizing a file with a
        /// user-supplied correction.
        /// </summary>
        public string ResultId { get; set; }

        /// <summary>
        /// Gets or sets the ID of the movie to organize.
        /// </summary>
        public string MovieId { get; set; }

        /// <summary>
        /// Gets or sets the movie name. Only required if this movie has not yet been added to the library, otherwise
        /// this can be null.
        /// </summary>
        public string NewMovieName { get; set; }

        /// <summary>
        /// Gets or sets the movie year. Only required if this movie has not yet been added to the library, otherwise
        /// this can be null.
        /// </summary>
        public int? NewMovieYear { get; set; }

        /// <summary>
        /// Gets or sets the base target folder of the media library.
        /// </summary>
        public string TargetFolder { get; set; }

        /// <summary>
        /// Gets or sets the provider IDs for the series. Only required if this movie has not yet been added to the
        /// library, otherwise this can be empty.
        /// </summary>
        public IReadOnlyDictionary<string, string> NewMovieProviderIds { get; set; }
    }
}
