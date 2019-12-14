using System;
using System.Collections.Generic;

namespace Emby.AutoOrganize.Model
{
    public class AutoOrganizeOptions
    {
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
        public List<SmartMatchInfo> SmartMatchInfos { get; set; }

        public bool Converted { get; set; }

        public AutoOrganizeOptions()
        {
            TvOptions = new TvFileOrganizationOptions();
            MovieOptions = new MovieFileOrganizationOptions();
            SmartMatchInfos = new List<SmartMatchInfo>();
            Converted = false;
        }
    }
}
