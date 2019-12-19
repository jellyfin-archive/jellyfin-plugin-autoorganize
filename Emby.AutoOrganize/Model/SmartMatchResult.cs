using System;
using System.Collections.Generic;

namespace Emby.AutoOrganize.Model
{
    public class SmartMatchResult
    {
        public Guid Id { get; set; }

        public string ItemName { get; set; }
        
        public string DisplayName { get; set; }
        
        public FileOrganizerType OrganizerType { get; set; }

        public List<string> MatchStrings { get; }

        public SmartMatchResult()
        {
            Id = Guid.NewGuid();
            MatchStrings = new List<string>();
        }
    }
}
