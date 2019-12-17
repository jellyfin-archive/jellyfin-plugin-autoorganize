using System;
using System.Collections.Generic;

namespace Emby.AutoOrganize.Model
{
    public class SmartMatchResult
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="SmartMatchResult"/> class.
        /// </summary>
        public SmartMatchResult()
        {
            Id = Guid.NewGuid();
            MatchStrings = new List<string>();
        }

        public Guid Id { get; set; }

        public string ItemName { get; set; }

        public string DisplayName { get; set; }

        public FileOrganizerType OrganizerType { get; set; }

        public List<string> MatchStrings { get; set; }
    }
}
