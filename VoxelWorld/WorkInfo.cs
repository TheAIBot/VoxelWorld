using System;

namespace VoxelWorld
{
    internal readonly struct WorkInfo
    {
        private readonly VoxelGridHierarchy WorkItem;
        private readonly VoxelSystemData GenData;
        private readonly GridPos Pos;
        private readonly VoxelType VType;

        public WorkInfo(VoxelGridHierarchy gridHir, VoxelSystemData genData, GridPos pos, VoxelType type)
        {
            this.WorkItem = gridHir;
            this.GenData = genData;
            this.Pos = pos;
            this.VType = type;
        }

        public void DoWork(VoxelGrid grid)
        {
            if (VType == VoxelType.Grid)
            {
                WorkItem.EndGeneratingGrid(GenData, grid, Pos);
            }
            else if (VType == VoxelType.Hierarchy)
            {
                WorkItem.EndGeneratingHierarchy(GenData, grid, Pos);
            }
            else
            {
                throw new Exception("");
            }
        }
    }
}
