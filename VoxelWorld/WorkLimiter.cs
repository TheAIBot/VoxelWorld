using System;
using System.Numerics;
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
        private static readonly ExecutionDataflowBlockOptions options = new ExecutionDataflowBlockOptions() { MaxDegreeOfParallelism = 6 };
        private static readonly ActionBlock<WorkInfo> DoWork = new ActionBlock<WorkInfo>(x => x.DoWork(), options);

        public static void QueueWork(WorkInfo work)
        {
            DoWork.Post(work);
        }
    }
}
