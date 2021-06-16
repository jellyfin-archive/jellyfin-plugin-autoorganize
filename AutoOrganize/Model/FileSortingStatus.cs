namespace Emby.AutoOrganize.Model
{
    /// <summary>
    /// The result status of sorting a single item.
    /// </summary>
    public enum FileSortingStatus
    {
        /// <summary>
        /// File sorting completed successfully.
        /// </summary>
        Success,

        /// <summary>
        /// File sorting failed due to an error.
        /// </summary>
        Failure,

        /// <summary>
        /// File sorting was skipped on this item because it already exists in the target library.
        /// </summary>
        SkippedExisting
    }
}
