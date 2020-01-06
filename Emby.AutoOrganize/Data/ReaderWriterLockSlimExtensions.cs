// TODO: These extension methods should be removed and replaced with the SemaphoreSlim usage in the main repo:
// https://github.com/jellyfin/jellyfin/blob/master/Emby.Server.Implementations/Data/BaseSqliteRepository.cs#L91
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
#pragma warning disable SA1600 // Elements should be documented

using System;
using System.Collections.Generic;
using System.Threading;

namespace Emby.AutoOrganize.Data
{
    public static class ReaderWriterLockSlimExtensions
    {
        public static IDisposable Read(this ReaderWriterLockSlim obj)
        {
            // if (BaseSqliteRepository.ThreadSafeMode > 0)
            // {
            //    return new DummyToken();
            // }
            return new WriteLockToken(obj);
        }

        public static IDisposable Write(this ReaderWriterLockSlim obj)
        {
            // if (BaseSqliteRepository.ThreadSafeMode > 0)
            // {
            //    return new DummyToken();
            // }
            return new WriteLockToken(obj);
        }

        private sealed class ReadLockToken : IDisposable
        {
            private ReaderWriterLockSlim _sync;

            public ReadLockToken(ReaderWriterLockSlim sync)
            {
                _sync = sync;
                sync.EnterReadLock();
            }

            public void Dispose()
            {
                if (_sync != null)
                {
                    _sync.ExitReadLock();
                    _sync = null;
                }
            }
        }

        private sealed class WriteLockToken : IDisposable
        {
            private ReaderWriterLockSlim _sync;

            public WriteLockToken(ReaderWriterLockSlim sync)
            {
                _sync = sync;
                sync.EnterWriteLock();
            }

            public void Dispose()
            {
                if (_sync != null)
                {
                    _sync.ExitWriteLock();
                    _sync = null;
                }
            }
        }
    }
}
