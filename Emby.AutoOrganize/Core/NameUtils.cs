using System;
using System.Globalization;
using System.Text.RegularExpressions;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Extensions;
using MediaBrowser.Model.Extensions;

namespace Emby.AutoOrganize.Core
{
    public static class NameUtils
    {
        private static readonly CultureInfo UsCulture = new CultureInfo("en-US");

        internal static int GetMatchScore(string sortedName, int? year, string itemName, int? itemProductionYear)
        {
            var score = 0;

            var seriesNameWithoutYear = itemName;
            if (itemProductionYear.HasValue)
            {
                seriesNameWithoutYear = seriesNameWithoutYear.Replace(itemProductionYear.Value.ToString(UsCulture), String.Empty);
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

        internal static Tuple<T, int> GetMatchScore<T>(string sortedName, int? year, T item)
            where T : BaseItem
        {
            return new Tuple<T, int>(item, GetMatchScore(sortedName, year, item.Name, item.ProductionYear));
        }


        private static bool IsNameMatch(string name1, string name2)
        {
            name1 = GetComparableName(name1);
            name2 = GetComparableName(name2);

            return String.Equals(name1, name2, StringComparison.OrdinalIgnoreCase);
        }

        private static string GetComparableName(string name)
        {
            name = name.RemoveDiacritics();

            name = " " + name + " ";

            name = name.Replace(".", " ")
            .Replace("_", " ")
            .Replace(" and ", " ")
            .Replace(".and.", " ")
            .Replace("&", " ")
            .Replace("!", " ")
            .Replace("(", " ")
            .Replace(")", " ")
            .Replace(":", " ")
            .Replace(",", " ")
            .Replace("-", " ")
            .Replace("'", " ")
            .Replace("[", " ")
            .Replace("]", " ");
            name = Regex.Replace(name, " a ", String.Empty, RegexOptions.IgnoreCase);
            name = Regex.Replace(name, " the ", String.Empty, RegexOptions.IgnoreCase);
            name = name.Replace(" ", String.Empty);

            return name.Trim();
        }
    }
}
