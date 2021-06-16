using System.Collections.Generic;
using System.Net.Mime;
using System.Threading.Tasks;
using AutoOrganize.Core;
using AutoOrganize.Model;
using MediaBrowser.Model.Dto;
using MediaBrowser.Model.Querying;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace AutoOrganize.Api
{
    /// <summary>
    /// The file organization controller.
    /// </summary>
    [ApiController]
    [Authorize(Policy = "RequiresElevation")]
    [Route("Library/FileOrganizations")]
    [Produces(MediaTypeNames.Application.Json)]
    public class FileOrganizationController : ControllerBase
    {
        private static IFileOrganizationService InternalFileOrganizationService
            => PluginEntryPoint.Current.FileOrganizationService;

        /// <summary>
        /// Gets file organization results.
        /// </summary>
        /// <param name="startIndex">Optional. The record index to start at. All items with a lower index will be dropped from the results.</param>
        /// <param name="limit">Optional. The maximum number of records to return.</param>
        /// <response code="204">Organization result returned.</response>
        /// <returns>A <see cref="QueryResult{FileOrganizationResult}"/>.</returns>
        [HttpGet]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public ActionResult<QueryResult<FileOrganizationResult>> Get(int? startIndex, int? limit)
        {
            var result = InternalFileOrganizationService.GetResults(new FileOrganizationResultQuery
            {
                Limit = limit,
                StartIndex = startIndex
            });

            return result;
        }

        /// <summary>
        /// Deletes the original file of a organizer result.
        /// </summary>
        /// <param name="id">The result id.</param>
        /// <response code="204">Original file deleted.</response>
        /// <returns>A <see cref="NoContentResult"/> indicating success.</returns>
        [HttpDelete("{id}/File")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        public ActionResult Delete([FromRoute] string id)
        {
            var task = InternalFileOrganizationService.DeleteOriginalFile(id);

            Task.WaitAll(task);

            return NoContent();
        }

        /// <summary>
        /// Clears the activity log.
        /// </summary>
        /// <response code="204">Activity log cleared.</response>
        /// <returns>A <see cref="NoContentResult"/> indicating success.</returns>
        [HttpDelete]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        public ActionResult ClearActivityLog()
        {
            var task = InternalFileOrganizationService.ClearLog();

            Task.WaitAll(task);

            return NoContent();
        }

        /// <summary>
        /// Clears the activity log.
        /// </summary>
        /// <returns>A <see cref="NoContentResult"/> indicating success.</returns>
        [HttpDelete("Completed")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        public ActionResult ClearCompletedActivityLog()
        {
            var task = InternalFileOrganizationService.ClearCompleted();

            Task.WaitAll(task);

            return NoContent();
        }

        /// <summary>
        /// Performs an organization.
        /// </summary>
        /// <param name="id">Result id.</param>
        /// <response code="204">Performing organization.</response>
        /// <returns>A <see cref="NoContentResult"/> indicating success.</returns>
        [HttpPost("{id}/Organize")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        public ActionResult PerformOrganization([FromRoute] string id)
        {
            // Don't await this
            var task = InternalFileOrganizationService.PerformOrganization(id);

            // Async processing (close dialog early instead of waiting until the file has been copied)
            // Wait 2s for exceptions that may occur to have them forwarded to the client for immediate error display
            task.Wait(2000);

            return NoContent();
        }

        /// <summary>
        /// Performs organization of a tv episode.
        /// </summary>
        /// <param name="id">Result id.</param>
        /// <param name="seriesId">Series id.</param>
        /// <param name="seasonNumber">Season number.</param>
        /// <param name="episodeNumber">Episode number.</param>
        /// <param name="endingEpisodeNumber">Ending episode number.</param>
        /// <param name="newSeriesName">Name of a series to add.</param>
        /// <param name="newSeriesYear">Year of a series to add.</param>
        /// <param name="newSeriesProviderIds">A list of provider IDs identifying a new series.</param>
        /// <param name="rememberCorrection">Whether or not to apply the same correction to future episodes of the same series.</param>
        /// <param name="targetFolder">Target folder.</param>
        /// <response code="204">Organization performed successfully.</response>
        /// <returns>An <see cref="NoContentResult"/> indicating success.</returns>
        [HttpPost("{id}/Episode/Organize")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        public ActionResult OrganizeEpisode(
            [FromRoute] string id,
            [FromQuery] string seriesId,
            [FromQuery] int seasonNumber,
            [FromQuery] int episodeNumber,
            [FromQuery] int? endingEpisodeNumber,
            [FromQuery] string newSeriesName,
            [FromQuery] int? newSeriesYear,
            [FromQuery] Dictionary<string, string> newSeriesProviderIds,
            [FromQuery] bool rememberCorrection,
            [FromQuery] string targetFolder)
        {
            // Don't await this
            var task = InternalFileOrganizationService.PerformOrganization(new EpisodeFileOrganizationRequest
            {
                EndingEpisodeNumber = endingEpisodeNumber,
                EpisodeNumber = episodeNumber,
                RememberCorrection = rememberCorrection,
                ResultId = id,
                SeasonNumber = seasonNumber,
                SeriesId = seriesId,
                NewSeriesName = newSeriesName,
                NewSeriesYear = newSeriesYear,
                NewSeriesProviderIds = newSeriesProviderIds ?? new Dictionary<string, string>(),
                TargetFolder = targetFolder
            });

            // Async processing (close dialog early instead of waiting until the file has been copied)
            // Wait 2s for exceptions that may occur to have them forwarded to the client for immediate error display
            task.Wait(2000);

            return NoContent();
        }

        /// <summary>
        /// Performs organization of a movie.
        /// </summary>
        /// <param name="id">Result id.</param>
        /// <param name="movieId">Movie id.</param>
        /// <param name="newMovieName">Name of a movie to add.</param>
        /// <param name="newMovieYear">Year of a movie to add.</param>
        /// <param name="newMovieProviderIds">A list of provider IDs identifying a new movie.</param>
        /// <param name="targetFolder">Target Folder.</param>
        /// <response code="204">Organization performed successfully.</response>
        /// <returns>A <see cref="NoContentResult"/> indicating success.</returns>
        [HttpPost("{id}/Movie/Organize")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        public ActionResult OrganizeMovie(
            [FromRoute] string id,
            [FromQuery] string movieId,
            [FromQuery] string newMovieName,
            [FromQuery] int? newMovieYear,
            [FromQuery] Dictionary<string, string> newMovieProviderIds,
            [FromQuery] string targetFolder)
        {
            // Don't await this
            var task = InternalFileOrganizationService.PerformOrganization(new MovieFileOrganizationRequest
            {
                ResultId = id,
                MovieId = movieId,
                NewMovieName = newMovieName,
                NewMovieYear = newMovieYear,
                NewMovieProviderIds = newMovieProviderIds ?? new Dictionary<string, string>(),
                TargetFolder = targetFolder
            });

            // Async processing (close dialog early instead of waiting until the file has been copied)
            // Wait 2s for exceptions that may occur to have them forwarded to the client for immediate error display
            task.Wait(2000);

            return NoContent();
        }

        /// <summary>
        /// Gets smart match entries.
        /// </summary>
        /// <param name="startIndex">Optional. The record index to start at. All items with a lower index will be dropped from the results.</param>
        /// <param name="limit">Optional. The maximum number of records to return.</param>
        /// <response code="20�0">Smart watch entries returned.</response>
        /// <returns>A <see cref="QueryResult{SmartWatchResult}"/>.</returns>
        [HttpGet("SmartMatches")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public ActionResult<QueryResult<SmartMatchResult>> GetSmartMatchInfos(int? startIndex, int? limit)
        {
            var result = InternalFileOrganizationService.GetSmartMatchInfos(new FileOrganizationResultQuery
            {
                Limit = limit,
                StartIndex = startIndex
            });

            return result;
        }

        /// <summary>
        /// Deletes a smart match entry.
        /// </summary>
        /// <param name="entries">SmartMatch Entry.</param>
        /// <response code="204">Smart watch entry deleted.</response>
        /// <returns>A <see cref="NoContentResult"/> indicating success.</returns>
        [HttpPost("SmartMatches/Delete")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        public ActionResult DeleteSmartWatchEntry([FromQuery] IReadOnlyList<NameValuePair> entries)
        {
            foreach (var entry in entries)
            {
                InternalFileOrganizationService.DeleteSmartMatchEntry(entry.Name, entry.Value);
            }

            return NoContent();
        }
    }
}
