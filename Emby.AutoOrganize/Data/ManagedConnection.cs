using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SQLitePCL.pretty;

namespace Emby.AutoOrganize.Data
{
    /// <summary>
    /// Wraps a <see cref="SQLiteDatabaseConnection"/>.
    /// </summary>
    public sealed class ManagedConnection : IDisposable
    {
        private readonly SQLiteDatabaseConnection db;

        /// <summary>
        /// Initializes a new instance of the <see cref="ManagedConnection"/> class by wrapping a
        /// <see cref="SQLiteDatabaseConnection"/>. The caller of this constructor is responsible for disposing the
        /// connection at an appropriate time.
        /// </summary>
        /// <param name="db">The database connection to wrap.</param>
        public ManagedConnection(SQLiteDatabaseConnection db)
        {
            this.db = db;
        }

        public IStatement PrepareStatement(string sql)
        {
            return db.PrepareStatement(sql);
        }

        public IEnumerable<IStatement> PrepareAll(string sql)
        {
            return db.PrepareAll(sql);
        }

        public void ExecuteAll(string sql)
        {
            db.ExecuteAll(sql);
        }

        public void Execute(string sql, params object[] values)
        {
            db.Execute(sql, values);
        }

        public void RunQueries(string[] sql)
        {
            db.RunQueries(sql);
        }

        public void RunInTransaction(Action<IDatabaseConnection> action, TransactionMode mode)
        {
            db.RunInTransaction(action, mode);
        }

        public T RunInTransaction<T>(Func<IDatabaseConnection, T> action, TransactionMode mode)
        {
            return db.RunInTransaction(action, mode);
        }

        public IEnumerable<IReadOnlyList<IResultSetValue>> Query(string sql)
        {
            return db.Query(sql);
        }

        public IEnumerable<IReadOnlyList<IResultSetValue>> Query(string sql, params object[] values)
        {
            return db.Query(sql, values);
        }

        public void Dispose()
        {
            // There is nothing to dispose in this class, the db connection is managed by BaseSqliteRepository.
            // The IDisposable interface has been left on this class to reduce the amount of code changes necessary
            // later on in case we decide to move management of the db connection down to this class.
        }
    }
}
