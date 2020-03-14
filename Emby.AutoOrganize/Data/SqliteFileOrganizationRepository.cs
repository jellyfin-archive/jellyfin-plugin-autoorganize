using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Emby.AutoOrganize.Model;
using MediaBrowser.Controller;
using MediaBrowser.Model.Querying;
using MediaBrowser.Model.Serialization;
using Microsoft.Extensions.Logging;
using SQLitePCL.pretty;

namespace Emby.AutoOrganize.Data
{
    /// <summary>
    /// SQLite repository for managing persistence of <see cref="FileOrganizationResult"/> and
    /// <see cref="SmartMatchResult"/> entities.
    /// </summary>
    public class SqliteFileOrganizationRepository : BaseSqliteRepository, IFileOrganizationRepository
    {
        private readonly CultureInfo _usCulture = new CultureInfo("en-US");
        private readonly IJsonSerializer _jsonSerializer;

        /// <summary>
        /// Initializes a new instance of the <see cref="SqliteFileOrganizationRepository"/> class.
        /// </summary>
        /// <param name="logger">The application logger.</param>
        /// <param name="appPaths">The server application paths.</param>
        /// <param name="jsonSerializer">A JSON serializer.</param>
        public SqliteFileOrganizationRepository(
            ILogger logger,
            IServerApplicationPaths appPaths,
            IJsonSerializer jsonSerializer)
            : base(logger)
        {
            _jsonSerializer = jsonSerializer;
            DbFilePath = Path.Combine(appPaths.DataPath, "fileorganization.db");
        }

        /// <summary>
        /// Opens the connection to the database.
        /// </summary>
        public void Initialize()
        {
            using (var connection = CreateConnection())
            {
                RunDefaultInitialization(connection);

                string[] queries =
                {
                    "create table if not exists FileOrganizerResults (ResultId GUID PRIMARY KEY, OriginalPath TEXT, TargetPath TEXT, FileLength INT, OrganizationDate datetime, Status TEXT, OrganizationType TEXT, StatusMessage TEXT, ExtractedName TEXT, ExtractedYear int null, ExtractedSeasonNumber int null, ExtractedEpisodeNumber int null, ExtractedEndingEpisodeNumber, DuplicatePaths TEXT int null)",
                    "create index if not exists idx_FileOrganizerResults on FileOrganizerResults(ResultId)",
                    "create table if not exists SmartMatch (Id GUID PRIMARY KEY, ItemName TEXT, DisplayName TEXT, OrganizerType TEXT, MatchStrings TEXT null)",
                    "create index if not exists idx_SmartMatch on SmartMatch(Id)",
                };

                connection.RunQueries(queries);
            }
        }

        /// <inheritdoc/>
        public void SaveResult(FileOrganizationResult result, CancellationToken cancellationToken)
        {
            if (result == null)
            {
                throw new ArgumentNullException(nameof(result));
            }

            cancellationToken.ThrowIfCancellationRequested();

            using (WriteLock.Write())
            {
                using (var connection = CreateConnection())
                {
                    connection.RunInTransaction(
                        db =>
                        {
                            var commandText = "replace into FileOrganizerResults (ResultId, OriginalPath, TargetPath, FileLength, OrganizationDate, Status, OrganizationType, StatusMessage, ExtractedName, ExtractedYear, ExtractedSeasonNumber, ExtractedEpisodeNumber, ExtractedEndingEpisodeNumber, DuplicatePaths) values (@ResultId, @OriginalPath, @TargetPath, @FileLength, @OrganizationDate, @Status, @OrganizationType, @StatusMessage, @ExtractedName, @ExtractedYear, @ExtractedSeasonNumber, @ExtractedEpisodeNumber, @ExtractedEndingEpisodeNumber, @DuplicatePaths)";

                            using (var statement = db.PrepareStatement(commandText))
                            {
                                statement.TryBind("@ResultId", result.Id.ToGuidBlob());
                                statement.TryBind("@OriginalPath", result.OriginalPath);

                                statement.TryBind("@TargetPath", result.TargetPath);
                                statement.TryBind("@FileLength", result.FileSize);
                                statement.TryBind("@OrganizationDate", result.Date.ToDateTimeParamValue());
                                statement.TryBind("@Status", result.Status.ToString());
                                statement.TryBind("@OrganizationType", result.Type.ToString());
                                statement.TryBind("@StatusMessage", result.StatusMessage);
                                statement.TryBind("@ExtractedName", result.ExtractedName);
                                statement.TryBind("@ExtractedYear", result.ExtractedYear);
                                statement.TryBind("@ExtractedSeasonNumber", result.ExtractedSeasonNumber);
                                statement.TryBind("@ExtractedEpisodeNumber", result.ExtractedEpisodeNumber);
                                statement.TryBind("@ExtractedEndingEpisodeNumber", result.ExtractedEndingEpisodeNumber);
                                statement.TryBind("@DuplicatePaths", string.Join("|", result.DuplicatePaths.ToArray()));

                                statement.MoveNext();
                            }
                        },
                        TransactionMode);
                }
            }
        }

        /// <inheritdoc/>
        public Task Delete(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                throw new ArgumentNullException(nameof(id));
            }

            using (WriteLock.Write())
            {
                using (var connection = CreateConnection())
                {
                    connection.RunInTransaction(
                        db =>
                        {
                            string command = "delete from FileOrganizerResults where ResultId = @ResultId";
                            using (var statement = db.PrepareStatement(command))
                            {
                                statement.TryBind("@ResultId", id.ToGuidBlob());
                                statement.MoveNext();
                            }
                        },
                        TransactionMode);
                }
            }

            return Task.CompletedTask;
        }

        /// <inheritdoc/>
        public Task DeleteAll()
        {
            using (WriteLock.Write())
            {
                using (var connection = CreateConnection())
                {
                    connection.RunInTransaction(
                        db => db.Execute("delete from FileOrganizerResults"),
                        TransactionMode);
                }
            }

            return Task.CompletedTask;
        }

        /// <inheritdoc/>
        public Task DeleteCompleted()
        {
            using (WriteLock.Write())
            {
                using (var connection = CreateConnection())
                {
                    connection.RunInTransaction(
                        db =>
                        {
                            string command = "delete from FileOrganizerResults where Status = @Status";
                            using (var statement = db.PrepareStatement(command))
                            {
                                statement.TryBind("@Status", FileSortingStatus.Success.ToString());
                                statement.MoveNext();
                            }
                        },
                        TransactionMode);
                }
            }

            return Task.CompletedTask;
        }

        /// <inheritdoc/>
        public QueryResult<FileOrganizationResult> GetResults(FileOrganizationResultQuery query)
        {
            if (query == null)
            {
                throw new ArgumentNullException(nameof(query));
            }

            using (WriteLock.Read())
            {
                using (var connection = CreateConnection(true))
                {
                    var commandText = "SELECT ResultId, OriginalPath, TargetPath, FileLength, OrganizationDate, Status, OrganizationType, StatusMessage, ExtractedName, ExtractedYear, ExtractedSeasonNumber, ExtractedEpisodeNumber, ExtractedEndingEpisodeNumber, DuplicatePaths from FileOrganizerResults";

                    if (query.StartIndex.HasValue && query.StartIndex.Value > 0)
                    {
                        var startIndex = query.StartIndex.Value.ToString(_usCulture);
                        commandText += $" WHERE ResultId NOT IN (SELECT ResultId FROM FileOrganizerResults ORDER BY OrganizationDate DESC LIMIT {startIndex})";
                    }

                    commandText += " ORDER BY OrganizationDate DESC";

                    if (query.Limit.HasValue)
                    {
                        commandText += " LIMIT " + query.Limit.Value.ToString(_usCulture);
                    }

                    var list = new List<FileOrganizationResult>();

                    using (var statement = connection.PrepareStatement(commandText))
                    {
                        foreach (var row in statement.ExecuteQuery())
                        {
                            list.Add(GetResult(row));
                        }
                    }

                    int count;
                    using (var statement = connection.PrepareStatement("select count (ResultId) from FileOrganizerResults"))
                    {
                        count = statement.ExecuteQuery().SelectScalarInt().First();
                    }

                    return new QueryResult<FileOrganizationResult>()
                    {
                        Items = list.ToArray(),
                        TotalRecordCount = count
                    };
                }
            }
        }

        /// <inheritdoc/>
        public FileOrganizationResult GetResult(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                throw new ArgumentNullException(nameof(id));
            }

            using (WriteLock.Read())
            {
                using (var connection = CreateConnection(true))
                {
                    using (var statement = connection.PrepareStatement("select ResultId, OriginalPath, TargetPath, FileLength, OrganizationDate, Status, OrganizationType, StatusMessage, ExtractedName, ExtractedYear, ExtractedSeasonNumber, ExtractedEpisodeNumber, ExtractedEndingEpisodeNumber, DuplicatePaths from FileOrganizerResults where ResultId=@ResultId"))
                    {
                        statement.TryBind("@ResultId", id.ToGuidBlob());

                        foreach (var row in statement.ExecuteQuery())
                        {
                            return GetResult(row);
                        }
                    }

                    return null;
                }
            }
        }

        private FileOrganizationResult GetResult(IReadOnlyList<IResultSetValue> reader)
        {
            var index = 0;

            var result = new FileOrganizationResult
            {
                Id = reader[0].ReadGuidFromBlob().ToString("N", CultureInfo.InvariantCulture)
            };

            index++;
            if (reader[index].SQLiteType != SQLiteType.Null)
            {
                result.OriginalPath = reader[index].ToString();
            }

            index++;
            if (reader[index].SQLiteType != SQLiteType.Null)
            {
                result.TargetPath = reader[index].ToString();
            }

            index++;
            result.FileSize = reader[index].ToInt64();

            index++;
            result.Date = reader[index].ReadDateTime();

            index++;
            result.Status = (FileSortingStatus)Enum.Parse(typeof(FileSortingStatus), reader[index].ToString(), true);

            index++;
            result.Type = (FileOrganizerType)Enum.Parse(typeof(FileOrganizerType), reader[index].ToString(), true);

            index++;
            if (reader[index].SQLiteType != SQLiteType.Null)
            {
                result.StatusMessage = reader[index].ToString();
            }

            result.OriginalFileName = Path.GetFileName(result.OriginalPath);

            index++;
            if (reader[index].SQLiteType != SQLiteType.Null)
            {
                result.ExtractedName = reader[index].ToString();
            }

            index++;
            if (reader[index].SQLiteType != SQLiteType.Null)
            {
                result.ExtractedYear = reader[index].ToInt();
            }

            index++;
            if (reader[index].SQLiteType != SQLiteType.Null)
            {
                result.ExtractedSeasonNumber = reader[index].ToInt();
            }

            index++;
            if (reader[index].SQLiteType != SQLiteType.Null)
            {
                result.ExtractedEpisodeNumber = reader[index].ToInt();
            }

            index++;
            if (reader[index].SQLiteType != SQLiteType.Null)
            {
                result.ExtractedEndingEpisodeNumber = reader[index].ToInt();
            }

            index++;
            if (reader[index].SQLiteType != SQLiteType.Null)
            {
                result.DuplicatePaths = reader[index].ToString().Split('|').Where(i => !string.IsNullOrEmpty(i)).ToList();
            }

            return result;
        }

        /// <inheritdoc/>
        public void SaveResult(SmartMatchResult result, CancellationToken cancellationToken)
        {
            if (result == null)
            {
                throw new ArgumentNullException(nameof(result));
            }

            cancellationToken.ThrowIfCancellationRequested();

            using (WriteLock.Write())
            {
                using (var connection = CreateConnection())
                {
                    connection.RunInTransaction(
                        db =>
                        {
                            var commandText = "replace into SmartMatch (Id, ItemName, DisplayName, OrganizerType, MatchStrings) values (@Id, @ItemName, @DisplayName, @OrganizerType, @MatchStrings)";

                            using (var statement = db.PrepareStatement(commandText))
                            {
                                statement.TryBind("@Id", result.Id.ToGuidBlob());

                                statement.TryBind("@ItemName", result.ItemName);
                                statement.TryBind("@DisplayName", result.DisplayName);
                                statement.TryBind("@OrganizerType", result.OrganizerType.ToString());
                                statement.TryBind("@MatchStrings", _jsonSerializer.SerializeToString(result.MatchStrings));

                                statement.MoveNext();
                            }
                        },
                        TransactionMode);
                }
            }
        }

        /// <inheritdoc/>
        public void DeleteSmartMatch(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                throw new ArgumentNullException(nameof(id));
            }

            using (WriteLock.Write())
            {
                using (var connection = CreateConnection())
                {
                    connection.RunInTransaction(
                        db =>
                        {
                            using (var statement = db.PrepareStatement("delete from SmartMatch where Id = @Id"))
                            {
                                statement.TryBind("@Id", id.ToGuidBlob());
                                statement.MoveNext();
                            }
                        },
                        TransactionMode);
                }
            }
        }

        /// <inheritdoc/>
        public Task DeleteSmartMatch(string id, string matchString)
        {
            if (string.IsNullOrEmpty(id))
            {
                throw new ArgumentNullException(nameof(id));
            }

            var match = GetSmartMatch(id);

            match.MatchStrings.Remove(matchString);

            if (match.MatchStrings.Any())
            {
                SaveResult(match, CancellationToken.None);
            }
            else
            {
                DeleteSmartMatch(id);
            }

            return Task.CompletedTask;
        }

        /// <inheritdoc/>
        public void DeleteAllSmartMatch()
        {
            using (WriteLock.Write())
            {
                using (var connection = CreateConnection())
                {
                    connection.RunInTransaction(
                        db => db.Execute("delete from SmartMatch"),
                        TransactionMode);
                }
            }
        }

        private SmartMatchResult GetSmartMatch(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                throw new ArgumentNullException(nameof(id));
            }

            using (WriteLock.Read())
            {
                using (var connection = CreateConnection(true))
                {
                    using (var statement = connection.PrepareStatement("SELECT Id, ItemName, DisplayName, OrganizerType, MatchStrings from SmartMatch where Id=@Id"))
                    {
                        statement.TryBind("@Id", id.ToGuidBlob());

                        foreach (var row in statement.ExecuteQuery())
                        {
                            return GetResultSmartMatch(row);
                        }
                    }

                    return null;
                }
            }
        }

        /// <inheritdoc/>
        public QueryResult<SmartMatchResult> GetSmartMatch(FileOrganizationResultQuery query)
        {
            if (query == null)
            {
                throw new ArgumentNullException(nameof(query));
            }

            using (WriteLock.Read())
            {
                using (var connection = CreateConnection(true))
                {
                    var commandText = "SELECT Id, ItemName, DisplayName, OrganizerType, MatchStrings from SmartMatch";

                    if (query.StartIndex.HasValue && query.StartIndex.Value > 0)
                    {
                        var startIndex = query.StartIndex.Value.ToString(_usCulture);
                        commandText += $" WHERE Id NOT IN (SELECT Id FROM SmartMatch ORDER BY ItemName DESC LIMIT {startIndex})";
                    }

                    commandText += " ORDER BY ItemName DESC";

                    if (query.Limit.HasValue)
                    {
                        commandText += " LIMIT " + query.Limit.Value.ToString(_usCulture);
                    }

                    var list = new List<SmartMatchResult>();

                    using (var statement = connection.PrepareStatement(commandText))
                    {
                        foreach (var row in statement.ExecuteQuery())
                        {
                            list.Add(GetResultSmartMatch(row));
                        }
                    }

                    int count;
                    using (var statement = connection.PrepareStatement("select count (Id) from SmartMatch"))
                    {
                        count = statement.ExecuteQuery().SelectScalarInt().First();
                    }

                    return new QueryResult<SmartMatchResult>()
                    {
                        Items = list.ToArray(),
                        TotalRecordCount = count
                    };
                }
            }
        }

        private SmartMatchResult GetResultSmartMatch(IReadOnlyList<IResultSetValue> reader)
        {
            var index = 0;

            var result = new SmartMatchResult
            {
                Id = reader[0].ReadGuidFromBlob()
            };

            index++;
            result.ItemName = reader[index].ToString();

            index++;
            result.DisplayName = reader[index].ToString();

            index++;
            result.OrganizerType = (FileOrganizerType)Enum.Parse(typeof(FileOrganizerType), reader[index].ToString(), true);

            index++;
            if (reader[index].SQLiteType != SQLiteType.Null)
            {
                result.MatchStrings.AddRange(_jsonSerializer.DeserializeFromString<List<string>>(reader[index].ToString()));
            }

            return result;
        }
    }
}
