using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace Emby.AutoOrganize.Model
{
    /// <summary>
    /// Configuration options for the <see cref="AutoOrganizePlugin"/>.
    /// </summary>
    public class AutoOrganizeOptions
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="AutoOrganizeOptions"/> class.
        /// </summary>
        public AutoOrganizeOptions()
        {
            TvOptions = new TvFileOrganizationOptions();
            MovieOptions = new MovieFileOrganizationOptions();
            Converted = false;
        }

        /// <summary>
        /// Gets or sets options used to auto-organize TV media.
        /// </summary>
        public TvFileOrganizationOptions TvOptions { get; set; }

        /// <summary>
        /// Gets or sets the options used to auto-organize movie media.
        /// </summary>
        public MovieFileOrganizationOptions MovieOptions { get; set; }

        /// <summary>
        /// Gets or sets a list of smart match entries.
        /// </summary>
        [Obsolete("This configuration is now stored in the SQLite database.")]
        [SuppressMessage("Usage", "CA2227:Collection properties should be read only", Justification = "This property needs to support serialization by both ServiceStack and XmlSerializer")]
        public List<SmartMatchInfo> SmartMatchInfos { get; set; } = new List<SmartMatchInfo>();

        /// <summary>
        /// Gets or sets a value indicating whether the smart match info has been moved from this configuration into
        /// the SQLite database.
        /// </summary>
        public bool Converted { get; set; }
    }
}
