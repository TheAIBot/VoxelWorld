using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks.Dataflow;

namespace VoxelWorld
{
    internal readonly struct WorkInfo
    {
        private readonly object WorkItem;
        private readonly VoxelSystemData GenData;
        private readonly Vector3 RotatedLookDir;

        public WorkInfo(VoxelGridInfo grid, VoxelSystemData genData, Vector3 rotLookDir)
        {
            this.WorkItem = grid;
            this.GenData = genData;
            this.RotatedLookDir = rotLookDir;
        }

        public WorkInfo(VoxelHierarchyInfo hir, VoxelSystemData genData, Vector3 rotLookDir)
        {
            this.WorkItem = hir;
            this.GenData = genData;
            this.RotatedLookDir = rotLookDir;
        }

        public void DoWork()
        {
            if (WorkItem is VoxelGridInfo grid)
            {
                grid.EndGenerating(GenData, RotatedLookDir);
            }
            else if (WorkItem is VoxelHierarchyInfo hir)
            {
                hir.EndGenerating(GenData, RotatedLookDir);
            }
            else
            {
                throw new Exception("");
            }
        }
    }

    internal static class WorkLimiter
    {
        private static readonly BlockingCollection<WorkInfo> Work = new BlockingCollection<WorkInfo>();
        private static readonly List<Thread> Workers = new List<Thread>();
        private static CancellationTokenSource CancelSource = new CancellationTokenSource();

        public static void QueueWork(WorkInfo work)
        {
            Work.Add(work);
        }

        public static void StartWorkers()
        {
            for (int i = 0; i < 6; i++)
            {
                Workers.Add(new Thread(() =>
                {
                    try
                    {
                        while (!CancelSource.Token.IsCancellationRequested)
                        {
                            var work = Work.Take(CancelSource.Token);
                            work.DoWork();
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
