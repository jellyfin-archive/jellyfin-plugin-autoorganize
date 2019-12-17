using System;
using System.Collections.Generic;

namespace Emby.AutoOrganize.Model
{
    public class SmartMatchInfo
    {
        public SmartMatchInfo()
        {
            MatchStrings = new List<string>();
        }

        public string ItemName { get; set; }

        public string DisplayName { get; set; }

        public FileOrganizerType OrganizerType { get; set; }

        public List<string> MatchStrings { get; set; }
    }
}
