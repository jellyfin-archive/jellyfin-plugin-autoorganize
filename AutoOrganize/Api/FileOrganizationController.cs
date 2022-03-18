using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
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
        public async Task<ActionResult> Delete([FromRoute] string id)
        {
            await InternalFileOrganizationService.DeleteOriginalFile(id)
                .ConfigureAwait(false);

            return NoContent();
        }

        /// <summary>
        /// Clears the activity log.
        /// </summary>
        /// <response code="204">Activity log cleared.</response>
        /// <returns>A <see cref="NoContentResult"/> indicating success.</returns>
        [HttpDelete]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        public async Task<ActionResult> ClearActivityLog()
        {
            await InternalFileOrganizationService.ClearLog()
                .ConfigureAwait(false);

            return NoContent();
        }

        /// <summary>
        /// Clears the activity log.
        /// </summary>
        /// <returns>A <see cref="NoContentResult"/> indicating success.</returns>
        [HttpDelete("Completed")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        public async Task<ActionResult> ClearCompletedActivityLog()
        {
            await InternalFileOrganizationService.ClearCompleted()
                .ConfigureAwait(false);

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
        /// <param name="request">The request body.</param>
        /// <response code="204">Organization performed successfully.</response>
        /// <returns>An <see cref="NoContentResult"/> indicating success.</returns>
        [HttpPost("{id}/Episode/Organize")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        public ActionResult OrganizeEpisode(
            [FromRoute] string id,
            [FromBody, Required] EpisodeFileOrganizationRequest request)
        {
            request.ResultId = id;
            // Don't await this
            var task = InternalFileOrganizationService.PerformOrganization(request);

            // Async processing (close dialog early instead of waiting until the file has been copied)
            // Wait 2s for exceptions that may occur to have them forwarded to the client for immediate error display
            task.Wait(2000);

            return NoContent();
        }

        /// <summary>
        /// Performs organization of a movie.
        /// </summary>
        /// <param name="id">Result id.</param>
        /// <param name="request">The request.</param>
        /// <response code="204">Organization performed successfully.</response>
        /// <returns>A <see cref="NoContentResult"/> indicating success.</returns>
        [HttpPost("{id}/Movie/Organize")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        public ActionResult OrganizeMovie(
            [FromRoute] string id,
            [FromBody, Required] MovieFileOrganizationRequest request)
        {
            // Don't await this
            request.ResultId = id;
            var task = InternalFileOrganizationService.PerformOrganization(request);

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
        /// <response code="200">Smart watch entries returned.</response>
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
