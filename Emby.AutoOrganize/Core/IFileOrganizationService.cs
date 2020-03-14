using System;
using System.Threading;
using System.Threading.Tasks;
using Emby.AutoOrganize.Model;
using MediaBrowser.Model.Events;
using MediaBrowser.Model.Querying;

namespace Emby.AutoOrganize.Core
{
    /// <summary>
    /// A service that can be used to auto-organize media files.
    /// </summary>
    public interface IFileOrganizationService
    {
        /// <summary>
        /// Occurs when a new <see cref="FileOrganizationResult"/> record has been created.
        /// </summary>
        event EventHandler<GenericEventArgs<FileOrganizationResult>> ItemAdded;

        /// <summary>
        /// Occurs when a <see cref="FileOrganizationResult"/> record has been updated.
        /// </summary>
        event EventHandler<GenericEventArgs<FileOrganizationResult>> ItemUpdated;

        /// <summary>
        /// Occurs when a <see cref="FileOrganizationResult"/> record has been deleted.
        /// </summary>
        event EventHandler<GenericEventArgs<FileOrganizationResult>> ItemRemoved;

        /// <summary>
        /// Occurs when multiple <see cref="FileOrganizationResult"/> records are deleted.
        /// </summary>
        event EventHandler LogReset;

        /// <summary>
        /// Processes the new files.
        /// </summary>
        void BeginProcessNewFiles();

        /// <summary>
        /// Deletes the original file.
        /// </summary>
        /// <param name="resultId">The result identifier.</param>
        /// <returns>Task.</returns>
        Task DeleteOriginalFile(string resultId);

        /// <summary>
        /// Clears the log.
        /// </summary>
        /// <returns>Task.</returns>
        Task ClearLog();

        /// <summary>
        /// Clears the log.
        /// </summary>
        /// <returns>Task.</returns>
        Task ClearCompleted();

        /// <summary>
        /// Performs the organization.
        /// </summary>
        /// <param name="resultId">The result identifier.</param>
        /// <returns>Task.</returns>
        Task PerformOrganization(string resultId);

        /// <summary>
        /// Performs the episode organization.
        /// </summary>
        /// <param name="request">The request.</param>
        /// <returns>Task.</returns>
        Task PerformOrganization(EpisodeFileOrganizationRequest request);

        /// <summary>
        /// Performs the episode organization.
        /// </summary>
        /// <param name="request">The request.</param>
        /// <returns>Task.</returns>
        Task PerformOrganization(MovieFileOrganizationRequest request);

        /// <summary>
        /// Gets the results.
        /// </summary>
        /// <param name="query">The query.</param>
        /// <returns>IEnumerable{FileOrganizationResult}.</returns>
        QueryResult<FileOrganizationResult> GetResults(FileOrganizationResultQuery query);

        /// <summary>
        /// Gets the result.
        /// </summary>
        /// <param name="id">The identifier.</param>
        /// <returns>FileOrganizationResult.</returns>
        FileOrganizationResult GetResult(string id);

        /// <summary>
        /// Gets the result by source path.
        /// </summary>
        /// <param name="path">The path.</param>
        /// <returns>FileOrganizationResult.</returns>
        FileOrganizationResult GetResultBySourcePath(string path);

        /// <summary>
        /// Saves the result.
        /// </summary>
        /// <param name="result">The result.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        void SaveResult(FileOrganizationResult result, CancellationToken cancellationToken);

        /// <summary>
        /// Saves the result.
        /// </summary>
        /// <param name="result">The result.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        void SaveResult(SmartMatchResult result, CancellationToken cancellationToken);

        /// <summary>
        /// Returns a list of smart match entries.
        /// </summary>
        /// <param name="query">The query.</param>
        /// <returns>IEnumerable{SmartMatchInfo}.</returns>
        QueryResult<SmartMatchResult> GetSmartMatchInfos(FileOrganizationResultQuery query);

        /// <summary>
        /// Returns a list of smart match entries.
        /// </summary>
        /// <returns>IEnumerable{SmartMatchInfo}.</returns>
        QueryResult<SmartMatchResult> GetSmartMatchInfos();

        /// <summary>
        /// Deletes a match string entry from a <see cref="SmartMatchResult"/>. If there are no match strings remaining
        /// then the <see cref="SmartMatchResult"/> itself will also be deleted.
        /// </summary>
        /// <param name="id">The id of the <see cref="SmartMatchResult"/> to delete.</param>
        /// <param name="matchString">The match string to delete.</param>
        void DeleteSmartMatchEntry(string id, string matchString);

        /// <summary>
        /// Attempts to add an item to the list of currently processed items.
        /// </summary>
        /// <param name="result">The result item.</param>
        /// <param name="fullClientRefresh">Passing true will notify the client to reload all items, otherwise only a single item will be refreshed.</param>
        /// <returns>True if the item was added, False if the item is already contained in the list.</returns>
        bool AddToInProgressList(FileOrganizationResult result, bool fullClientRefresh);

        /// <summary>
        /// Removes an item from the list of currently processed items.
        /// </summary>
        /// <param name="result">The result item.</param>
        /// <returns>True if the item was removed, False if the item was not contained in the list.</returns>
        bool RemoveFromInprogressList(FileOrganizationResult result);
    }
}
