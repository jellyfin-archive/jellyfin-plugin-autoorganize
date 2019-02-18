namespace Jellyfin.AutoOrganize.Model
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
        public SmartMatchInfo[] SmartMatchInfos { get; set; }

        public bool Converted { get; set; }

        public AutoOrganizeOptions()
        {
            TvOptions = new TvFileOrganizationOptions();
            MovieOptions = new MovieFileOrganizationOptions();
            SmartMatchInfos = new SmartMatchInfo[] { };
            Converted = false;
        }
    }
}
