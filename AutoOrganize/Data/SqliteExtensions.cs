// TODO: This class should be replaced with the common class in the main Jellyfin repository:
// https://github.com/jellyfin/jellyfin/blob/master/Emby.Server.Implementations/Data/SqliteExtensions.cs
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using SQLitePCL.pretty;

namespace AutoOrganize.Data
{
    /// <summary>
    /// Static class containing extension methods useful for working with a SQLite database.
    /// </summary>
    public static class SqliteExtensions
    {
        /// <summary>
        /// An array of ISO-8601 DateTime formats that we support parsing.
        /// </summary>
        private static readonly string[] _datetimeFormats = new string[]
        {
          "THHmmssK",
          "THHmmK",
          "HH:mm:ss.FFFFFFFK",
          "HH:mm:ssK",
          "HH:mmK",
          "yyyy-MM-dd HH:mm:ss.FFFFFFFK", /* NOTE: UTC default (5). */
          "yyyy-MM-dd HH:mm:ssK",
          "yyyy-MM-dd HH:mmK",
          "yyyy-MM-ddTHH:mm:ss.FFFFFFFK",
          "yyyy-MM-ddTHH:mmK",
          "yyyy-MM-ddTHH:mm:ssK",
          "yyyyMMddHHmmssK",
          "yyyyMMddHHmmK",
          "yyyyMMddTHHmmssFFFFFFFK",
          "THHmmss",
          "THHmm",
          "HH:mm:ss.FFFFFFF",
          "HH:mm:ss",
          "HH:mm",
          "yyyy-MM-dd HH:mm:ss.FFFFFFF", /* NOTE: Non-UTC default (19). */
          "yyyy-MM-dd HH:mm:ss",
          "yyyy-MM-dd HH:mm",
          "yyyy-MM-ddTHH:mm:ss.FFFFFFF",
          "yyyy-MM-ddTHH:mm",
          "yyyy-MM-ddTHH:mm:ss",
          "yyyyMMddHHmmss",
          "yyyyMMddHHmm",
          "yyyyMMddTHHmmssFFFFFFF",
          "yyyy-MM-dd",
          "yyyyMMdd",
          "yy-MM-dd"
        };

        private static string _datetimeFormatUtc = _datetimeFormats[5];
        private static string _datetimeFormatLocal = _datetimeFormats[19];

        public static void RunQueries(this SQLiteDatabaseConnection connection, string[] queries)
        {
            if (queries == null)
            {
                throw new ArgumentNullException(nameof(queries));
            }

            connection.RunInTransaction(conn =>
            {
                conn.ExecuteAll(string.Join(';', queries));
            });
        }

        public static byte[] ToGuidBlob(this string str)
        {
            return ToGuidBlob(new Guid(str));
        }

        public static byte[] ToGuidBlob(this Guid guid)
        {
            return guid.ToByteArray();
        }

        public static Guid ReadGuidFromBlob(this ResultSetValue result)
        {
            return new Guid(result.ToBlob().ToArray());
        }

        public static string ToDateTimeParamValue(this DateTime dateValue)
        {
            var kind = DateTimeKind.Utc;

            return (dateValue.Kind == DateTimeKind.Unspecified)
                ? DateTime.SpecifyKind(dateValue, kind).ToString(
                    GetDateTimeKindFormat(kind),
                    CultureInfo.InvariantCulture)
                : dateValue.ToString(
                    GetDateTimeKindFormat(dateValue.Kind),
                    CultureInfo.InvariantCulture);
        }

        private static string GetDateTimeKindFormat(
           DateTimeKind kind)
        {
            return (kind == DateTimeKind.Utc) ? _datetimeFormatUtc : _datetimeFormatLocal;
        }

        public static DateTime ReadDateTime(this ResultSetValue result)
        {
            var dateText = result.ToString();

            return DateTime.ParseExact(
                dateText,
                _datetimeFormats,
                DateTimeFormatInfo.InvariantInfo,
                DateTimeStyles.None).ToUniversalTime();
        }

        public static void Attach(ManagedConnection db, string path, string alias)
        {
            var commandText = $"attach @path as {alias};";

            using (var statement = db.PrepareStatement(commandText))
            {
                statement.TryBind("@path", path);
                statement.MoveNext();
            }
        }

        public static bool IsDBNull(this IReadOnlyList<ResultSetValue> result, int index)
        {
            return result[index].SQLiteType == SQLiteType.Null;
        }

        public static string GetString(this IReadOnlyList<ResultSetValue> result, int index)
        {
            return result[index].ToString();
        }

        public static bool GetBoolean(this IReadOnlyList<ResultSetValue> result, int index)
        {
            return result[index].ToBool();
        }

        public static int GetInt32(this IReadOnlyList<ResultSetValue> result, int index)
        {
            return result[index].ToInt();
        }

        public static long GetInt64(this IReadOnlyList<ResultSetValue> result, int index)
        {
            return result[index].ToInt64();
        }

        public static float GetFloat(this IReadOnlyList<ResultSetValue> result, int index)
        {
            return result[index].ToFloat();
        }

        public static Guid GetGuid(this IReadOnlyList<ResultSetValue> result, int index)
        {
            return result[index].ReadGuidFromBlob();
        }

        [SuppressMessage("Microsoft.Performance", "CA1801:MustUseParameter", MessageId = "name", Justification = "Used only in debug.")]
        private static void ThrowInvalidParamName(string name)
        {
#if DEBUG
            throw new Exception("Invalid param name: " + name);
#endif
        }

        public static void TryBind(this IStatement statement, string name, double value)
        {
            IBindParameter bindParam;
            if (statement.BindParameters.TryGetValue(name, out bindParam))
            {
                bindParam.Bind(value);
            }
            else
            {
                ThrowInvalidParamName(name);
            }
        }

        public static void TryBind(this IStatement statement, string name, string value)
        {
            IBindParameter bindParam;
            if (statement.BindParameters.TryGetValue(name, out bindParam))
            {
                if (value == null)
                {
                    bindParam.BindNull();
                }
                else
                {
                    bindParam.Bind(value);
                }
            }
            else
            {
                ThrowInvalidParamName(name);
            }
        }

        public static void TryBind(this IStatement statement, string name, bool value)
        {
            IBindParameter bindParam;
            if (statement.BindParameters.TryGetValue(name, out bindParam))
            {
                bindParam.Bind(value);
            }
            else
            {
                ThrowInvalidParamName(name);
            }
        }

        public static void TryBind(this IStatement statement, string name, float value)
        {
            IBindParameter bindParam;
            if (statement.BindParameters.TryGetValue(name, out bindParam))
            {
                bindParam.Bind(value);
            }
            else
            {
                ThrowInvalidParamName(name);
            }
        }

        public static void TryBind(this IStatement statement, string name, int value)
        {
            IBindParameter bindParam;
            if (statement.BindParameters.TryGetValue(name, out bindParam))
            {
                bindParam.Bind(value);
            }
            else
            {
                ThrowInvalidParamName(name);
            }
        }

        public static void TryBind(this IStatement statement, string name, Guid value)
        {
            IBindParameter bindParam;
            if (statement.BindParameters.TryGetValue(name, out bindParam))
            {
                bindParam.Bind(value.ToGuidBlob());
            }
            else
            {
                ThrowInvalidParamName(name);
            }
        }

        public static void TryBind(this IStatement statement, string name, DateTime value)
        {
            IBindParameter bindParam;
            if (statement.BindParameters.TryGetValue(name, out bindParam))
            {
                bindParam.Bind(value.ToDateTimeParamValue());
            }
            else
            {
                ThrowInvalidParamName(name);
            }
        }

        public static void TryBind(this IStatement statement, string name, long value)
        {
            IBindParameter bindParam;
            if (statement.BindParameters.TryGetValue(name, out bindParam))
            {
                bindParam.Bind(value);
            }
            else
            {
                ThrowInvalidParamName(name);
            }
        }

        public static void TryBind(this IStatement statement, string name, byte[] value)
        {
            IBindParameter bindParam;
            if (statement.BindParameters.TryGetValue(name, out bindParam))
            {
                bindParam.Bind(value);
            }
            else
            {
                ThrowInvalidParamName(name);
            }
        }

        public static void TryBindNull(this IStatement statement, string name)
        {
            IBindParameter bindParam;
            if (statement.BindParameters.TryGetValue(name, out bindParam))
            {
                bindParam.BindNull();
            }
            else
            {
                ThrowInvalidParamName(name);
            }
        }

        public static void TryBind(this IStatement statement, string name, DateTime? value)
        {
            if (value.HasValue)
            {
                TryBind(statement, name, value.Value);
            }
            else
            {
                TryBindNull(statement, name);
            }
        }

        public static void TryBind(this IStatement statement, string name, Guid? value)
        {
            if (value.HasValue)
            {
                TryBind(statement, name, value.Value);
            }
            else
            {
                TryBindNull(statement, name);
            }
        }

        public static void TryBind(this IStatement statement, string name, double? value)
        {
            if (value.HasValue)
            {
                TryBind(statement, name, value.Value);
            }
            else
            {
                TryBindNull(statement, name);
            }
        }

        public static void TryBind(this IStatement statement, string name, int? value)
        {
            if (value.HasValue)
            {
                TryBind(statement, name, value.Value);
            }
            else
            {
                TryBindNull(statement, name);
            }
        }

        public static void TryBind(this IStatement statement, string name, float? value)
        {
            if (value.HasValue)
            {
                TryBind(statement, name, value.Value);
            }
            else
            {
                TryBindNull(statement, name);
            }
        }

        public static void TryBind(this IStatement statement, string name, bool? value)
        {
            if (value.HasValue)
            {
                TryBind(statement, name, value.Value);
            }
            else
            {
                TryBindNull(statement, name);
            }
        }

        public static IEnumerable<IReadOnlyList<ResultSetValue>> ExecuteQuery(this IStatement statement)
        {
            while (statement.MoveNext())
            {
                yield return statement.Current;
            }
        }
    }
}
