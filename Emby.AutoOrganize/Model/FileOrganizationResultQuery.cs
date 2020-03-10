namespace Emby.AutoOrganize.Model
{
    public class FileOrganizationResultQuery
    {
        /// <summary>
        /// Gets or sets a value indicating the number of items to skips over in the query. Use to specify a page
        /// number.
        /// </summary>
        public int? StartIndex { get; set; }

        /// <summary>
        /// Gets or sets the maximum number of items to return. Use to specify a page size.
        /// </summary>
        public int? Limit { get; set; }
    }
}
