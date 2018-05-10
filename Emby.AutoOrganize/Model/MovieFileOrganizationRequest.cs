using System.Collections.Generic;

namespace Emby.AutoOrganize.Model
{
    public class MovieFileOrganizationRequest
    {
        public string ResultId { get; set; }
        
        public string MovieId { get; set; }

        public string NewMovieName { get; set; }

        public int? NewMovieYear { get; set; }

        public string TargetFolder { get; set; }

        public Dictionary<string, string> NewMovieProviderIds { get; set; }
    }
}