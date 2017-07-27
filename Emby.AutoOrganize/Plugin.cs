using System.Collections.Generic;
using Emby.AutoOrganize.Configuration;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Model.Plugins;
using MediaBrowser.Model.Serialization;

namespace Emby.AutoOrganize
{
    public class Plugin : BasePlugin<PluginConfiguration>, IHasWebPages
    {
        public Plugin(IApplicationPaths appPaths, IXmlSerializer xmlSerializer)
            : base(appPaths, xmlSerializer)
        {
        }

        public override string Name => "Auto Organize";


        public override string Description
            => "Automatically organize new media";

        public PluginConfiguration PluginConfiguration => Configuration;

        public IEnumerable<PluginPageInfo> GetPages()
        {
            return new[]
            {
                new PluginPageInfo
                {
                    Name = "AutoOrganizeLog",
                    EmbeddedResourcePath = GetType().Namespace + ".Configuration.autoorganizelog.html"
                },
                new PluginPageInfo
                {
                    Name = "AutoOrganizeSmart",
                    EmbeddedResourcePath = GetType().Namespace + ".Configuration.autoorganizesmart.html"
                },
                new PluginPageInfo
                {
                    Name = "AutoOrganizeTv",
                    EmbeddedResourcePath = GetType().Namespace + ".Configuration.autoorganizetv.html"
                },
                new PluginPageInfo
                {
                    Name = "AutoOrganizeLogJs",
                    EmbeddedResourcePath = GetType().Namespace + ".Configuration.autoorganizelog.js"
                },
                new PluginPageInfo
                {
                    Name = "AutoOrganizeSmartJs",
                    EmbeddedResourcePath = GetType().Namespace + ".Configuration.autoorganizesmart.js"
                },
                new PluginPageInfo
                {
                    Name = "AutoOrganizeTvJs",
                    EmbeddedResourcePath = GetType().Namespace + ".Configuration.autoorganizetv.js"
                },
                new PluginPageInfo
                {
                    Name = "FileOrganizerJs",
                    EmbeddedResourcePath = GetType().Namespace + ".Configuration.fileorganizer.js"
                },
                new PluginPageInfo
                {
                    Name = "FileOrganizerHtml",
                    EmbeddedResourcePath = GetType().Namespace + ".Configuration.fileorganizer.template.html"
                }
            };
        }
    }
}
