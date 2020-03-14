using System.Threading;
using System.Threading.Tasks;
using Emby.AutoOrganize.Model;
using MediaBrowser.Model.Querying;

namespace Emby.AutoOrganize.Data
{
    /// <summary>
    /// Repository for managing persistence of <see cref="FileOrganizationResult"/> and <see cref="SmartMatchResult"/>
    /// entities.
    /// </summary>
    public interface IFileOrganizationRepository
    {
        /// <summary>
        /// Saves the result.
        /// </summary>
        /// <param name="result">The result.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        void SaveResult(FileOrganizationResult result, CancellationToken cancellationToken);

        /// <summary>
        /// Deletes the specified identifier.
        /// </summary>
        /// <param name="id">The identifier.</param>
        /// <returns>A task representing the delete operation.</returns>
        Task Delete(string id);

        /// <summary>
        /// Gets the result.
        /// </summary>
        /// <param name="id">The identifier.</param>
        /// <returns>FileOrganizationResult.</returns>
        FileOrganizationResult GetResult(string id);

        /// <summary>
        /// Gets the results.
        /// </summary>
        /// <param name="query">The query.</param>
        /// <returns>IEnumerable{FileOrganizationResult}.</returns>
        QueryResult<FileOrganizationResult> GetResults(FileOrganizationResultQuery query);

        /// <summary>
        /// Deletes all.
        /// </summary>
        /// <returns>Task.</returns>
        Task DeleteAll();

        /// <summary>
        /// Deletes all.
        /// </summary>
        /// <returns>Task.</returns>
        Task DeleteCompleted();

        /// <summary>
        /// Save a smart match result.
        /// </summary>
        /// <param name="result">The result to save.</param>
        /// <param name="cancellationToken">A cancellation token for the operation.</param>
        void SaveResult(SmartMatchResult result, CancellationToken cancellationToken);

        /// <summary>
        /// Delete a smart match result by id.
        /// </summary>
        /// <param name="id">The id of the smart match result to delete.</param>
        void DeleteSmartMatch(string id);

        /// <summary>
        /// Remove a single match string from a smart match result.
        /// </summary>
        /// <param name="id">The id of the smart match result to remove the match string from.</param>
        /// <param name="matchString">The match string to remove.</param>
        /// <returns>A task representing the operation.</returns>
        Task DeleteSmartMatch(string id, string matchString);

        /// <summary>
        /// Delete all smart match result records.
        /// </summary>
        void DeleteAllSmartMatch();

        /// <summary>
        /// Query for a list of smart match result records.
        /// </summary>
        /// <param name="query">The query parameters.</param>
        /// <returns>The query result.</returns>
        QueryResult<SmartMatchResult> GetSmartMatch(FileOrganizationResultQuery query);
    }
}
