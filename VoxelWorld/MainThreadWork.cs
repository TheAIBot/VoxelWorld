using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace VoxelWorld
{
    internal static class MainThreadWork
    {
        private static ConcurrentQueue<(ManualResetEventSlim isDone, Action action)> WorkToDo = new ConcurrentQueue<(ManualResetEventSlim isDone, Action action)>();
        private static int MainThreadID = 0;

        private static BlockingCollection<int> dwa;

        public static void QueueWorkAndWait(Action action)
        {
            //main thread is not allowed to wait for itself so it will instead do the work immediatly
            if (MainThreadID == Thread.CurrentThread.ManagedThreadId)
            {
                action.Invoke();
                return;
            }

            ManualResetEventSlim isDone = new ManualResetEventSlim(false);

            WorkToDo.Enqueue((isDone, action));
            isDone.Wait();
            isDone.Dispose();
        }

        public static void QueueWork(Action action)
        {
            if (MainThreadID == Thread.CurrentThread.ManagedThreadId)
            {
                action.Invoke();
                return;
            }

            WorkToDo.Enqueue((null, action));
        }

        public static void ExecuteWork()
        {
            int workLimit = Math.Min(20000, WorkToDo.Count);
            for (int i = 0; i < workLimit; i++)
            {
                if (WorkToDo.TryDequeue(out var work))
                {
                    try
                    {
                        work.action.Invoke();
                    }
                    catch (Exception e)
                    {

                        throw;
                    }
                    work.isDone?.Set();
                }
            }
        }

        public static void SetThisThreadToMainThread()
        {
            MainThreadID = Thread.CurrentThread.ManagedThreadId;
        }
    }
}
