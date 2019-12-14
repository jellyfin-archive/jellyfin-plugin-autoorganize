using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Emby.AutoOrganize.Model;
using MediaBrowser.Common.Configuration;

namespace Emby.AutoOrganize.Core
{
    public static class ConfigurationExtension
    {
        public const string AutoOrganizeOptionsKey = "autoorganize";

        public static void Convert(this IConfigurationManager manager, IFileOrganizationService service)
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
                        OrganizerType = optionsSmartMatchInfo.OrganizerType
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
                    ConfigurationType = typeof (AutoOrganizeOptions)
                }
            };
        }
    }
}
