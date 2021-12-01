using System.Collections.Generic;
using AutoOrganize.Model;
using MediaBrowser.Common.Configuration;

namespace AutoOrganize.Core
{
    /// <summary>
    /// A configuration factory that registers the configuration entry required for the <see cref="AutoOrganizePlugin"/>.
    /// </summary>
    public class AutoOrganizeOptionsFactory : IConfigurationFactory
    {
        /// <inheritdoc/>
        public IEnumerable<ConfigurationStore> GetConfigurations()
        {
            return new List<ConfigurationStore>
            {
                new ConfigurationStore
                {
                    Key = ConfigurationExtensions.AutoOrganizeOptionsKey,
                    ConfigurationType = typeof(AutoOrganizeOptions)
                }
            };
        }
    }
}
