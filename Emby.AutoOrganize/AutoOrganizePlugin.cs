using System;
using System.Collections.Generic;
using Emby.AutoOrganize.Configuration;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Model.Plugins;
using MediaBrowser.Model.Serialization;

namespace Emby.AutoOrganize
{
    /// <summary>
    /// The auto-organize plugin.
    /// </summary>
    public class AutoOrganizePlugin : BasePlugin<PluginConfiguration>, IHasWebPages
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="AutoOrganizePlugin"/> class.
        /// </summary>
        /// <param name="appPaths">The application paths.</param>
        /// <param name="xmlSerializer">The XML serializer.</param>
        public AutoOrganizePlugin(IApplicationPaths appPaths, IXmlSerializer xmlSerializer)
            : base(appPaths, xmlSerializer)
        {
        }

        /// <inheritdoc/>
        public override Guid Id => new Guid("70b7b43b-471b-4159-b4be-56750c795499");

        /// <inheritdoc/>
        public override string Name => "Auto Organize";

        /// <inheritdoc/>
        public override string Description => "Automatically organize new media";

        /// <inheritdoc/>
        public IEnumerable<PluginPageInfo> GetPages()
        {
            return new[]
            {
                new PluginPageInfo
                {
                    Name = "AutoOrganizeLog",
                    EmbeddedResourcePath = GetType().Namespace + ".Configuration.autoorganizelog.html",
                    EnableInMainMenu = true
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
                    Name = "AutoOrganizeMovie",
                    EmbeddedResourcePath = GetType().Namespace + ".Configuration.autoorganizemovie.html"
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
                    Name = "AutoOrganizeMovieJs",
                    EmbeddedResourcePath = GetType().Namespace + ".Configuration.autoorganizemovie.js"
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
