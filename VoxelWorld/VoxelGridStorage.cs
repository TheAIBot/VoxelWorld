using System;
using System.Collections.Concurrent;
using System.Numerics;

namespace VoxelWorld
{
    internal static class VoxelGridStorage
    {
        private static readonly ConcurrentBag<VoxelGrid> Grids = new ConcurrentBag<VoxelGrid>();

        public static VoxelGrid GetGrid(Vector3 center, VoxelSystemData genData)
        {
            if (Grids.TryTake(out VoxelGrid grid))
            {
                grid.Repurpose(center, genData);
                return grid;
            }
            else
            {
                return new VoxelGrid(center, genData);
            }
        }

        public static void StoreForReuse(VoxelGrid grid)
        {
            if (Grids.Count < 1000)
            {
                Grids.Add(grid);
            }
        }
    }
}
