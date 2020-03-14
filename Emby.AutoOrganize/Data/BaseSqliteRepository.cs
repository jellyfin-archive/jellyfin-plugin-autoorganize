// TODO: This class should be replaced with the common class in the main Jellyfin repository:
// https://github.com/jellyfin/jellyfin/blob/master/Emby.Server.Implementations/Data/BaseSqliteRepository.cs
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using Microsoft.Extensions.Logging;
using SQLitePCL;
using SQLitePCL.pretty;

namespace Emby.AutoOrganize.Data
{
    public abstract class BaseSqliteRepository : IDisposable
    {
        private readonly ILogger _logger;
        private readonly object _disposeLock = new object();

        private static bool _versionLogged;
        private string _defaultWal;
        private SQLiteDatabaseConnection _dbConnection;
        private ManagedConnection _connection;
        private bool _disposed = false;

        static BaseSqliteRepository()
        {
            SQLite3.EnableSharedCache = false;

            int rc = raw.sqlite3_config(raw.SQLITE_CONFIG_MEMSTATUS, 0);

            rc = raw.sqlite3_config(raw.SQLITE_CONFIG_MULTITHREAD, 1);

            rc = raw.sqlite3_enable_shared_cache(1);

            ThreadSafeMode = raw.sqlite3_threadsafe();
        }

        protected BaseSqliteRepository(ILogger logger)
        {
            _logger = logger;
            WriteLock = new ReaderWriterLockSlim(LockRecursionPolicy.NoRecursion);
        }

        protected string DbFilePath { get; set; }

        protected ReaderWriterLockSlim WriteLock { get; }

        protected TransactionMode TransactionMode => TransactionMode.Deferred;

        protected TransactionMode ReadTransactionMode => TransactionMode.Deferred;

        internal static int ThreadSafeMode { get; set; }

        protected virtual bool EnableSingleConnection => true;

        protected virtual bool EnableTempStoreMemory => false;

        protected virtual int? CacheSize => null;

        protected ManagedConnection CreateConnection(bool isReadOnly = false)
        {
            if (_connection != null)
            {
                return _connection;
            }

            lock (WriteLock)
            {
                if (!_versionLogged)
                {
                    _versionLogged = true;
                    _logger.LogInformation("SQLite version: {Version}", SQLite3.Version);
                    _logger.LogInformation("SQLite compiler options: {Options}", string.Join(",", SQLite3.CompilerOptions));
                }

                ConnectionFlags connectionFlags;

                if (isReadOnly)
                {
                    // TODO: set connection flags correctly
                    // connectionFlags = ConnectionFlags.ReadOnly
                    _logger.LogDebug("Opening read connection to database");
                    connectionFlags = ConnectionFlags.Create;
                    connectionFlags |= ConnectionFlags.ReadWrite;
                }
                else
                {
                    _logger.LogDebug("Opening write connection to database.");
                    connectionFlags = ConnectionFlags.Create;
                    connectionFlags |= ConnectionFlags.ReadWrite;
                }

                if (EnableSingleConnection)
                {
                    connectionFlags |= ConnectionFlags.PrivateCache;
                }
                else
                {
                    connectionFlags |= ConnectionFlags.SharedCached;
                }

                connectionFlags |= ConnectionFlags.NoMutex;

                var db = SQLite3.Open(DbFilePath, connectionFlags, null);

                try
                {
                    if (string.IsNullOrWhiteSpace(_defaultWal))
                    {
                        _defaultWal = db.Query("PRAGMA journal_mode").SelectScalarString().First();

                        _logger.LogInformation("Default journal_mode for {0} is {1}", DbFilePath, _defaultWal);
                    }

                    var queries = new List<string>
                    {
                        // "PRAGMA cache size=-10000"
                        // "PRAGMA read_uncommitted = true",
                        "PRAGMA synchronous=Normal"
                    };

                    if (CacheSize.HasValue)
                    {
                        queries.Add("PRAGMA cache_size=" + CacheSize.Value.ToString(CultureInfo.InvariantCulture));
                    }

                    if (EnableTempStoreMemory)
                    {
                        queries.Add("PRAGMA temp_store = memory");
                    }
                    else
                    {
                        queries.Add("PRAGMA temp_store = file");
                    }

                    db.ExecuteAll(string.Join(";", queries.ToArray()));
                }
                catch
                {
                    db.Dispose();
                    throw;
                }

                _dbConnection = db;
                _connection = new ManagedConnection(db);
                return _connection;
            }
        }

        public IStatement PrepareStatement(ManagedConnection connection, string sql)
        {
            return connection.PrepareStatement(sql);
        }

        public IStatement PrepareStatementSafe(ManagedConnection connection, string sql)
        {
            return connection.PrepareStatement(sql);
        }

        public IStatement PrepareStatement(IDatabaseConnection connection, string sql)
        {
            return connection.PrepareStatement(sql);
        }

        public IStatement PrepareStatementSafe(IDatabaseConnection connection, string sql)
        {
            return connection.PrepareStatement(sql);
        }

        public List<IStatement> PrepareAll(IDatabaseConnection connection, IEnumerable<string> sql)
        {
            return PrepareAllSafe(connection, sql);
        }

        public List<IStatement> PrepareAllSafe(IDatabaseConnection connection, IEnumerable<string> sql)
        {
            return sql.Select(connection.PrepareStatement).ToList();
        }

        protected bool TableExists(ManagedConnection connection, string name)
        {
            return connection.RunInTransaction(db => TableExists(db, name), ReadTransactionMode);
        }

        protected bool TableExists(IDatabaseConnection db, string name)
        {
            using (var statement = PrepareStatement(db, "select DISTINCT tbl_name from sqlite_master"))
            {
                foreach (var row in statement.ExecuteQuery())
                {
                    if (string.Equals(name, row.GetString(0), StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        protected void RunDefaultInitialization(ManagedConnection db)
        {
            var queries = new List<string>
            {
                "PRAGMA journal_mode=WAL",
                "PRAGMA page_size=4096",
                "PRAGMA synchronous=Normal"
            };

            if (EnableTempStoreMemory)
            {
                queries.AddRange(new List<string>
                {
                    "pragma default_temp_store = memory",
                    "pragma temp_store = memory"
                });
            }
            else
            {
                queries.AddRange(new List<string>
                {
                    "pragma temp_store = file"
                });
            }

            db.ExecuteAll(string.Join(";", queries.ToArray()));
            _logger.LogInformation("PRAGMA synchronous={sync}", db.Query("PRAGMA synchronous").SelectScalarString().First());
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Releases unmanaged and - optionally - managed resources.
        /// </summary>
        /// <param name="disposing"><c>true</c> to release both managed and unmanaged resources; <c>false</c> to release only unmanaged resources.</param>
        protected virtual void Dispose(bool disposing)
        {
            if (_disposed)
            {
                return;
            }

            if (disposing)
            {
                // Dispose managed state
                DisposeConnection();
            }

            _disposed = true;
        }

        private void DisposeConnection()
        {
            try
            {
                lock (_disposeLock)
                {
                    using (WriteLock.Write())
                    {
                        _connection?.Dispose();
                        _dbConnection?.Dispose();
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error disposing database", ex);
            }
        }

        protected List<string> GetColumnNames(IDatabaseConnection connection, string table)
        {
            var list = new List<string>();

            foreach (var row in connection.Query("PRAGMA table_info(" + table + ")"))
            {
                if (row[1].SQLiteType != SQLiteType.Null)
                {
                    var name = row[1].ToString();

                    list.Add(name);
                }
            }

            return list;
        }

        protected void AddColumn(IDatabaseConnection connection, string table, string columnName, string type, List<string> existingColumnNames)
        {
            if (existingColumnNames.Contains(columnName, StringComparer.OrdinalIgnoreCase))
            {
                return;
            }

            connection.Execute("alter table " + table + " add column " + columnName + " " + type + " NULL");
        }
    }
}
