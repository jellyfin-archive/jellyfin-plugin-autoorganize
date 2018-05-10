using System.Collections.Generic;
using System.Threading.Tasks;
using Emby.AutoOrganize.Core;
using Emby.AutoOrganize.Model;
using MediaBrowser.Controller.Net;
using MediaBrowser.Model.Dto;
using MediaBrowser.Model.Querying;
using MediaBrowser.Model.Serialization;
using MediaBrowser.Model.Services;

namespace Emby.AutoOrganize.Api
{
    [Route("/Library/FileOrganization", "GET", Summary = "Gets file organization results")]
    public class GetFileOrganizationActivity : IReturn<QueryResult<FileOrganizationResult>>
    {
        /// <summary>
        /// Skips over a given number of items within the results. Use for paging.
        /// </summary>
        /// <value>The start index.</value>
        [ApiMember(Name = "StartIndex", Description = "Optional. The record index to start at. All items with a lower index will be dropped from the results.", IsRequired = false, DataType = "int", ParameterType = "query", Verb = "GET")]
        public int? StartIndex { get; set; }

        /// <summary>
        /// The maximum number of items to return
        /// </summary>
        /// <value>The limit.</value>
        [ApiMember(Name = "Limit", Description = "Optional. The maximum number of records to return", IsRequired = false, DataType = "int", ParameterType = "query", Verb = "GET")]
        public int? Limit { get; set; }
    }

    [Route("/Library/FileOrganizations", "DELETE", Summary = "Clears the activity log")]
    public class ClearOrganizationLog : IReturnVoid
    {
    }

    [Route("/Library/FileOrganizations/Completed", "DELETE", Summary = "Clears the activity log")]
    public class ClearOrganizationCompletedLog : IReturnVoid
    {
    }

    [Route("/Library/FileOrganizations/{Id}/File", "DELETE", Summary = "Deletes the original file of a organizer result")]
    public class DeleteOriginalFile : IReturnVoid
    {
        /// <summary>
        /// Gets or sets the id.
        /// </summary>
        /// <value>The id.</value>
        [ApiMember(Name = "Id", Description = "Result Id", IsRequired = true, DataType = "string", ParameterType = "path", Verb = "DELETE")]
        public string Id { get; set; }
    }

    [Route("/Library/FileOrganizations/{Id}/Organize", "POST", Summary = "Performs an organization")]
    public class PerformOrganization : IReturn<QueryResult<FileOrganizationResult>>
    {
        /// <summary>
        /// Gets or sets the id.
        /// </summary>
        /// <value>The id.</value>
        [ApiMember(Name = "Id", Description = "Result Id", IsRequired = true, DataType = "string", ParameterType = "path", Verb = "POST")]
        public string Id { get; set; }
    }

    [Route("/Library/FileOrganizations/{Id}/Episode/Organize", "POST", Summary = "Performs organization of a tv episode")]
    public class OrganizeEpisode
    {
        [ApiMember(Name = "Id", Description = "Result Id", IsRequired = true, DataType = "string", ParameterType = "path", Verb = "POST")]
        public string Id { get; set; }

        [ApiMember(Name = "SeriesId", Description = "Series Id", IsRequired = true, DataType = "string", ParameterType = "query", Verb = "POST")]
        public string SeriesId { get; set; }

        [ApiMember(Name = "SeasonNumber", IsRequired = true, DataType = "int", ParameterType = "query", Verb = "POST")]
        public int SeasonNumber { get; set; }

        [ApiMember(Name = "EpisodeNumber", IsRequired = true, DataType = "int", ParameterType = "query", Verb = "POST")]
        public int EpisodeNumber { get; set; }

        [ApiMember(Name = "EndingEpisodeNumber", IsRequired = false, DataType = "int", ParameterType = "query", Verb = "POST")]
        public int? EndingEpisodeNumber { get; set; }

        [ApiMember(Name = "RememberCorrection", Description = "Whether or not to apply the same correction to future episodes of the same series.", IsRequired = false, DataType = "bool", ParameterType = "query", Verb = "POST")]
        public bool RememberCorrection { get; set; }

        [ApiMember(Name = "NewSeriesProviderIds", Description = "A list of provider IDs identifying a new series.", IsRequired = false, DataType = "Dictionary<string, string>", ParameterType = "query", Verb = "POST")]
        public Dictionary<string, string> NewSeriesProviderIds { get; set; }

        [ApiMember(Name = "NewSeriesName", Description = "Name of a series to add.", IsRequired = false, DataType = "string", ParameterType = "query", Verb = "POST")]
        public string NewSeriesName { get; set; }

        [ApiMember(Name = "NewSeriesYear", Description = "Year of a series to add.", IsRequired = false, DataType = "string", ParameterType = "query", Verb = "POST")]
        public int? NewSeriesYear { get; set; }

        [ApiMember(Name = "TargetFolder", Description = "Target Folder", IsRequired = false, DataType = "string", ParameterType = "query", Verb = "POST")]
        public string TargetFolder { get; set; }
    }

    [Route("/Library/FileOrganizations/{Id}/Movie/Organize", "POST", Summary = "Performs organization of a movie")]
    public class OrganizeMovie
    {
        [ApiMember(Name = "Id", Description = "Result Id", IsRequired = true, DataType = "string", ParameterType = "path", Verb = "POST")]
        public string Id { get; set; }

        [ApiMember(Name = "MovieId", Description = "Movie Id", IsRequired = true, DataType = "string", ParameterType = "query", Verb = "POST")]
        public string MovieId { get; set; }

        [ApiMember(Name = "NewMovieProviderIds", Description = "A list of provider IDs identifying a new movie.", IsRequired = false, DataType = "Dictionary<string, string>", ParameterType = "query", Verb = "POST")]
        public Dictionary<string, string> NewMovieProviderIds { get; set; }

        [ApiMember(Name = "NewMovieName", Description = "Name of a movie to add.", IsRequired = false, DataType = "string", ParameterType = "query", Verb = "POST")]
        public string NewMovieName { get; set; }

        [ApiMember(Name = "NewMovieYear", Description = "Year of a movie to add.", IsRequired = false, DataType = "string", ParameterType = "query", Verb = "POST")]
        public int? NewMovieYear { get; set; }

        [ApiMember(Name = "TargetFolder", Description = "Target Folder", IsRequired = false, DataType = "string", ParameterType = "query", Verb = "POST")]
        public string TargetFolder { get; set; }
    }

    [Route("/Library/FileOrganizations/SmartMatches", "GET", Summary = "Gets smart match entries")]
    public class GetSmartMatchInfos : IReturn<QueryResult<SmartMatchInfo>>
    {
        /// <summary>
        /// Skips over a given number of items within the results. Use for paging.
        /// </summary>
        /// <value>The start index.</value>
        [ApiMember(Name = "StartIndex", Description = "Optional. The record index to start at. All items with a lower index will be dropped from the results.", IsRequired = false, DataType = "int", ParameterType = "query", Verb = "GET")]
        public int? StartIndex { get; set; }

        /// <summary>
        /// The maximum number of items to return
        /// </summary>
        /// <value>The limit.</value>
        [ApiMember(Name = "Limit", Description = "Optional. The maximum number of records to return", IsRequired = false, DataType = "int", ParameterType = "query", Verb = "GET")]
        public int? Limit { get; set; }
    }

    [Route("/Library/FileOrganizations/SmartMatches/Delete", "POST", Summary = "Deletes a smart match entry")]
    public class DeleteSmartMatchEntry
    {
        [ApiMember(Name = "Entries", Description = "SmartMatch Entry", IsRequired = true, DataType = "string", ParameterType = "query", Verb = "POST")]
        public List<NameValuePair> Entries { get; set; }
    }

    [Authenticated(Roles = "Admin")]
    public class FileOrganizationService : IService, IRequiresRequest
    {
        private readonly IHttpResultFactory _resultFactory;

        public IRequest Request { get; set; }

        public FileOrganizationService(IHttpResultFactory resultFactory)
        {
            _resultFactory = resultFactory;
        }

        private IFileOrganizationService InternalFileOrganizationService
        {
            get { return PluginEntryPoint.Current.FileOrganizationService; }
        }

        public object Get(GetFileOrganizationActivity request)
        {
            var result = InternalFileOrganizationService.GetResults(new FileOrganizationResultQuery
            {
                Limit = request.Limit,
                StartIndex = request.StartIndex
            });

            return _resultFactory.GetOptimizedResult(Request, result);
        }

        public void Delete(DeleteOriginalFile request)
        {
            var task = InternalFileOrganizationService.DeleteOriginalFile(request.Id);

            Task.WaitAll(task);
        }

        public void Delete(ClearOrganizationLog request)
        {
            var task = InternalFileOrganizationService.ClearLog();

            Task.WaitAll(task);
        }


        public void Delete(ClearOrganizationCompletedLog request)
        {
            var task = InternalFileOrganizationService.ClearCompleted();

            Task.WaitAll(task);
        }


        public void Post(PerformOrganization request)
        {
            // Don't await this
            var task = InternalFileOrganizationService.PerformOrganization(request.Id);

            // Async processing (close dialog early instead of waiting until the file has been copied)
            // Wait 2s for exceptions that may occur to have them forwarded to the client for immediate error display
            task.Wait(2000);
        }

        public void Post(OrganizeEpisode request)
        {
            var dicNewProviderIds = new Dictionary<string, string>();

            if (request.NewSeriesProviderIds != null)
            {
                dicNewProviderIds = request.NewSeriesProviderIds;
            }

            // Don't await this
            var task = InternalFileOrganizationService.PerformOrganization(new EpisodeFileOrganizationRequest
            {
                EndingEpisodeNumber = request.EndingEpisodeNumber,
                EpisodeNumber = request.EpisodeNumber,
                RememberCorrection = request.RememberCorrection,
                ResultId = request.Id,
                SeasonNumber = request.SeasonNumber,
                SeriesId = request.SeriesId,
                NewSeriesName = request.NewSeriesName,
                NewSeriesYear = request.NewSeriesYear,
                NewSeriesProviderIds = dicNewProviderIds,
                TargetFolder = request.TargetFolder
            });

            // Async processing (close dialog early instead of waiting until the file has been copied)
            // Wait 2s for exceptions that may occur to have them forwarded to the client for immediate error display
            task.Wait(2000);
        }

        public void Post(OrganizeMovie request)
        {
            var dicNewProviderIds = new Dictionary<string, string>();

            if (request.NewMovieProviderIds != null)
            {
                dicNewProviderIds = request.NewMovieProviderIds;
            }

            // Don't await this
            var task = InternalFileOrganizationService.PerformOrganization(new MovieFileOrganizationRequest
            {
                ResultId = request.Id,
                MovieId = request.MovieId,
                NewMovieName = request.NewMovieName,
                NewMovieYear = request.NewMovieYear,
                NewMovieProviderIds = dicNewProviderIds,
                TargetFolder = request.TargetFolder
            });

            // Async processing (close dialog early instead of waiting until the file has been copied)
            // Wait 2s for exceptions that may occur to have them forwarded to the client for immediate error display
            task.Wait(2000);
        }

        public object Get(GetSmartMatchInfos request)
        {
            var result = InternalFileOrganizationService.GetSmartMatchInfos(new FileOrganizationResultQuery
            {
                Limit = request.Limit,
                StartIndex = request.StartIndex
            });

            return _resultFactory.GetOptimizedResult(Request, result);
        }

        public void Post(DeleteSmartMatchEntry request)
        {
            foreach (var entry in request.Entries)
            {
                InternalFileOrganizationService.DeleteSmartMatchEntry(entry.Name, entry.Value);
            }
        }
    }
}
