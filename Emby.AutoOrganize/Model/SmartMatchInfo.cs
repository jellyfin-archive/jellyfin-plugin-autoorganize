#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace Emby.AutoOrganize.Model
{
    [Obsolete("This has been replaced by SmartMatchResult")]
    public class SmartMatchInfo
    {
        public SmartMatchInfo()
        {
            MatchStrings = new List<string>();
        }

        public string ItemName { get; set; }

        public string DisplayName { get; set; }

        public FileOrganizerType OrganizerType { get; set; }

        [SuppressMessage("Usage", "CA2227:Collection properties should be read only", Justification = "This property needs to support serialization by both ServiceStack and XmlSerializer")]
        public List<string> MatchStrings { get; set; }
    }
}
