using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Numerics;
using System.Threading;
using VoxelWorld.Voxel.Grid;
using VoxelWorld.Voxel.System;

namespace VoxelWorld
{
    internal static class WorkLimiter
    {
        private static readonly BlockingCollection<WorkInfo> Work = new BlockingCollection<WorkInfo>();
        private static readonly List<Thread> Workers = new List<Thread>();
        private static CancellationTokenSource CancelSource = new CancellationTokenSource();

        public static void QueueWork(WorkInfo work)
        {
            Work.Add(work);
        }

        public static void StartWorkers(VoxelSystemData voxelgenData)
        {
            int workerCount = Math.Max(1, Environment.ProcessorCount - 2);
            for (int i = 0; i < workerCount; i++)
            {
                Workers.Add(new Thread(() =>
                {
                    Thread.CurrentThread.Priority = ThreadPriority.BelowNormal;
                    VoxelGrid grid = new VoxelGrid(new Vector3(0, 0, 0), voxelgenData);
                    try
                    {
                        while (!CancelSource.Token.IsCancellationRequested)
                        {
                            var work = Work.Take(CancelSource.Token);
                            work.DoWork(grid);
                        }
                    }
                    catch (OperationCanceledException)
                    {
                    }

                }));
            }

            for (int i = 0; i < Workers.Count; i++)
            {
                Workers[i].Start();
            }
        }

        public static void StopWorkers()
        {
            CancelSource.Cancel();
            for (int i = 0; i < Workers.Count; i++)
            {
                Workers[i].Join();
            }
        }
    }
}
