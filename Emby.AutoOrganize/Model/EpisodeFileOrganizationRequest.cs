using System.Collections.Generic;

namespace Emby.AutoOrganize.Model
{
    public class EpisodeFileOrganizationRequest
    {
        public string ResultId { get; set; }
        
        public string SeriesId { get; set; }

        public int SeasonNumber { get; set; }

        public int EpisodeNumber { get; set; }

        public int? EndingEpisodeNumber { get; set; }

        public bool RememberCorrection { get; set; }
        public string NewSeriesName { get; set; }

        public int? NewSeriesYear { get; set; }

        public string TargetFolder { get; set; }

        public Dictionary<string, string> NewSeriesProviderIds { get; set; }
    }
}
