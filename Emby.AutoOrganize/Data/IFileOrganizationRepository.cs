using MediaBrowser.Model.Querying;
using System.Threading;
using System.Threading.Tasks;
using Emby.AutoOrganize.Model;

namespace Emby.AutoOrganize.Data
{
    public interface IFileOrganizationRepository
    {
        #region FileOrganizationResult

        /// <summary>
        /// Saves the result.
        /// </summary>
        /// <param name="result">The result.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>Task.</returns>
        Task SaveResult(FileOrganizationResult result, CancellationToken cancellationToken);

        /// <summary>
        /// Deletes the specified identifier.
        /// </summary>
        /// <param name="id">The identifier.</param>
        /// <returns>Task.</returns>
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

        #endregion

        #region SmartMatch

        Task SaveResult(SmartMatchResult result, CancellationToken cancellationToken);

        void DeleteSmartMatch(string id);

        Task DeleteSmartMatch(string id, string matchString);

        void DeleteAllSmartMatch();

        QueryResult<SmartMatchResult> GetSmartMatch(FileOrganizationResultQuery query);

        #endregion
    }
}
