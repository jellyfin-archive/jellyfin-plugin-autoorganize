using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading;
using Emby.AutoOrganize.Model;
using MediaBrowser.Common.Configuration;

namespace Emby.AutoOrganize.Core
{
    public static class ConfigurationExtension
    {
        /// <summary>
        /// The key to use with <see cref="IConfigurationManager"/> for storing the configuration of this plugin.
        /// </summary>
        public const string AutoOrganizeOptionsKey = "autoorganize";

        /// <summary>
        /// Perform a one-time migration of smart match info from the plugin configuration to the SQLite database.
        /// </summary>
        [SuppressMessage("Compiler", "CS0618:Type or member is obsolete", Justification = "This method is used to migrates configuration away from the obsolete property.")]
        public static void ConvertSmartMatchInfo(this IConfigurationManager manager, IFileOrganizationService service)
        {
            var options = manager.GetConfiguration<AutoOrganizeOptions>(AutoOrganizeOptionsKey);
            if (!options.Converted)
            {
                options.Converted = true;

                foreach (SmartMatchInfo optionsSmartMatchInfo in options.SmartMatchInfos)
                {
                    var result = new SmartMatchResult
                    {
                        DisplayName = optionsSmartMatchInfo.DisplayName,
                        ItemName = optionsSmartMatchInfo.ItemName,
                        OrganizerType = optionsSmartMatchInfo.OrganizerType,
                    };
                    result.MatchStrings.AddRange(optionsSmartMatchInfo.MatchStrings);
                    service.SaveResult(result, CancellationToken.None);
                }

                manager.SaveAutoOrganizeOptions(options);
            }
        }

        public static AutoOrganizeOptions GetAutoOrganizeOptions(this IConfigurationManager manager)
        {
            return manager.GetConfiguration<AutoOrganizeOptions>(AutoOrganizeOptionsKey);
        }

        public static void SaveAutoOrganizeOptions(this IConfigurationManager manager, AutoOrganizeOptions options)
        {
            manager.SaveConfiguration(AutoOrganizeOptionsKey, options);
        }
    }

    public class AutoOrganizeOptionsFactory : IConfigurationFactory
    {
        public IEnumerable<ConfigurationStore> GetConfigurations()
        {
            return new List<ConfigurationStore>
            {
                new ConfigurationStore
                {
                    Key = ConfigurationExtension.AutoOrganizeOptionsKey,
                    ConfigurationType = typeof(AutoOrganizeOptions)
                }
            };
        }
    }
}
