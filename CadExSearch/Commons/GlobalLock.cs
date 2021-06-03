using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

#nullable disable

namespace CadExSearch.Commons
{
    public class GlobalLock : IDisposable, IAsyncDisposable
    {
        private bool IsStateLocked { get; set; }
        public bool IsLocked { get; private set; }

        public ValueTask DisposeAsync()
        {
            return new(Task.Run(Unlock));
        }

        public void Dispose()
        {
            Unlock();
        }

        public static async Task<GlobalLock> LockAsync()
        {
            var factory = new TaskFactory(TaskCreationOptions.LongRunning, TaskContinuationOptions.None);
            return await factory.StartNew(() =>
            {
                var locker = new GlobalLock();
                locker.Lock();
                while (!locker.IsStateLocked)
                    Thread.Sleep(TimeSpan.FromSeconds(1));
                return locker;
            });
        }

        public static GlobalLock LockSync()
        {
            var locker = new GlobalLock();
            locker.Lock();
            while (!locker.IsStateLocked)
                Thread.Sleep(TimeSpan.FromSeconds(1));
            return locker;
        }

        private void Lock()
        {
            var thread = new Thread(() =>
            {
                if (IsStateLocked)
                    return;
                IsLocked = true;
                Stream stream = null;
                while (true)
                    try
                    {
                        stream = File.OpenWrite(".\\CEOCC.Cache.lock");
                        break;
                    }
                    catch
                    {
                    }

                IsStateLocked = true;
                while (IsStateLocked) Thread.Sleep(1);
                stream.Dispose();
            });
            thread.Start();
        }

        public void Unlock()
        {
            IsStateLocked = false;
        }
    }
}