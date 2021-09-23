using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks.Dataflow;

namespace VoxelWorld
{
    internal enum VoxelType
    {
        Grid,
        Hierarchy
    }

    internal readonly struct WorkInfo
    {
        private readonly VoxelGridHierarchy WorkItem;
        private readonly VoxelSystemData GenData;
        private readonly VoxelType VType;

        public WorkInfo(VoxelGridHierarchy gridHir, VoxelSystemData genData, VoxelType type)
        {
            this.WorkItem = gridHir;
            this.GenData = genData;
            this.VType = type;
        }

        public void DoWork(VoxelGrid grid)
        {
            if (VType == VoxelType.Grid)
            {
                WorkItem.EndGeneratingGrid(GenData, grid);
            }
            else if (VType == VoxelType.Hierarchy)
            {
                WorkItem.EndGeneratingHierarchy(GenData, grid);
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

        public static void StartWorkers(VoxelSystemData voxelgenData)
        {
            for (int i = 0; i < 14; i++)
            {
                Workers.Add(new Thread(() =>
                {
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
