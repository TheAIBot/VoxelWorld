using System;
using System.Collections.Concurrent;
using System.Threading;

namespace VoxelWorld
{
    internal static class MainThreadWork
    {
        private static ConcurrentQueue<Action> WorkToDo = new ConcurrentQueue<Action>();
        private static int MainThreadID = 0;

        public static void QueueWork(Action action)
        {
            if (MainThreadID == Thread.CurrentThread.ManagedThreadId)
            {
                action.Invoke();
                return;
            }

            WorkToDo.Enqueue(action);
        }

        public static void ExecuteWork()
        {
            int workLimit = Math.Min(50, WorkToDo.Count);
            for (int i = 0; i < workLimit; i++)
            {
                if (WorkToDo.TryDequeue(out var work))
                {
                    work.Invoke();
                }
            }
        }

        public static void SetThisThreadToMainThread()
        {
            MainThreadID = Thread.CurrentThread.ManagedThreadId;
        }
    }
}
