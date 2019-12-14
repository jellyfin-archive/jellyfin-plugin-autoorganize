using System;
using System.Collections.Generic;

namespace Emby.AutoOrganize.Model
{
    public class SmartMatchResult
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        public string ItemName { get; set; }
        
        public string DisplayName { get; set; }
        
        public FileOrganizerType OrganizerType { get; set; }

        public List<string> MatchStrings { get; set; } = new List<string>();
    }
}
