using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace Emby.AutoOrganize.Model
{
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
        /// Gets or sets the tv options.
        /// </summary>
        /// <value>The tv options.</value>
        public TvFileOrganizationOptions TvOptions { get; set; }

        /// <summary>
        /// Gets or sets the tv options.
        /// </summary>
        /// <value>The tv options.</value>
        public MovieFileOrganizationOptions MovieOptions { get; set; }

        /// <summary>
        /// Gets or sets a list of smart match entries.
        /// </summary>
        /// <value>The smart match entries.</value>
        [Obsolete("This configuration is now stored in the SQLite database.")]
        [SuppressMessage("Usage", "CA2227:Collection properties should be read only", Justification = "This property needs to support serialization by both ServiceStack and XmlSerializer")]
        public List<SmartMatchInfo> SmartMatchInfos { get; set; } = new List<SmartMatchInfo>();

        public bool Converted { get; set; }
    }
}
