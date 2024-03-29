using System;
using System.Globalization;
using Diacritics.Extensions;
using MediaBrowser.Controller.Entities;

namespace AutoOrganize.Core
{
    /// <summary>
    /// Static helper class containing methods to work with names.
    /// </summary>
    public static class NameUtils
    {
        /// <summary>
        /// Assign a score to a matched media item.
        /// </summary>
        /// <param name="sortedName">The sorted name.</param>
        /// <param name="year">The sorted year.</param>
        /// <param name="itemName">The item name.</param>
        /// <param name="itemProductionYear">The item production year.</param>
        /// <returns>The calculated score.</returns>
        internal static int GetMatchScore(string sortedName, int? year, string itemName, int? itemProductionYear)
        {
            var score = 0;

            var seriesNameWithoutYear = itemName;
            if (itemProductionYear.HasValue)
            {
                seriesNameWithoutYear = seriesNameWithoutYear.Replace(itemProductionYear.Value.ToString(CultureInfo.InvariantCulture), string.Empty, StringComparison.Ordinal);
            }

            if (IsNameMatch(sortedName, seriesNameWithoutYear))
            {
                score++;

                if (year.HasValue && itemProductionYear.HasValue)
                {
                    if (year.Value == itemProductionYear.Value)
                    {
                        score++;
                    }
                    else
                    {
                        // Regardless of name, return a 0 score if the years don't match
                        return 0;
                    }
                }
            }

            return score;
        }

        /// <summary>
        /// Assign a score to a matched media item.
        /// </summary>
        /// <typeparam name="T">The item type.</typeparam>
        /// <param name="sortedName">The sorted name.</param>
        /// <param name="year">The sorted year.</param>
        /// <param name="item">The item.</param>
        /// <returns>A tuple containing the item and the calculated score.</returns>
        internal static Tuple<T, int> GetMatchScore<T>(string sortedName, int? year, T item)
            where T : BaseItem
        {
            return new Tuple<T, int>(item, GetMatchScore(sortedName, year, item.Name, item.ProductionYear));
        }

        private static bool IsNameMatch(string name1, string name2)
        {
            name1 = GetComparableName(name1);
            name2 = GetComparableName(name2);

            return string.Equals(name1, name2, StringComparison.OrdinalIgnoreCase);
        }

        private static string GetComparableName(string name)
        {
            name = name.RemoveDiacritics();

            name = " " + name + " ";

            name = name.Replace('.', ' ')
                .Replace('_', ' ')
                .Replace(" and ", " ", StringComparison.OrdinalIgnoreCase)
                .Replace(".and.", " ", StringComparison.OrdinalIgnoreCase)
                .Replace('&', ' ')
                .Replace('!', ' ')
                .Replace('(', ' ')
                .Replace(')', ' ')
                .Replace(':', ' ')
                .Replace(',', ' ')
                .Replace('-', ' ')
                .Replace('\'', ' ')
                .Replace('[', ' ')
                .Replace(']', ' ')
                .Replace(" a ", string.Empty, StringComparison.OrdinalIgnoreCase)
                .Replace(" the ", string.Empty, StringComparison.OrdinalIgnoreCase)
                .Replace(" ", string.Empty, StringComparison.Ordinal);

            return name.Trim();
        }
    }
}
