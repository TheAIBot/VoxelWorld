using System;

namespace VoxelWorld
{
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
}
