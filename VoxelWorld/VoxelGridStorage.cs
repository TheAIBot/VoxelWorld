using System;
using System.Collections.Concurrent;
using System.Numerics;

namespace VoxelWorld
{
    internal static class VoxelGridStorage
    {
        private static readonly ConcurrentStack<VoxelGrid> Grids = new ConcurrentStack<VoxelGrid>();

        public static VoxelGrid GetGrid(int size, Vector3 center, float voxelSize, Func<Vector3, float> gen)
        {
            if (Grids.TryPop(out VoxelGrid grid))
            {
                grid.Repurpose(center, voxelSize);
                return grid;
            }
            else
            {
                return new VoxelGrid(size, center, voxelSize, gen);
            }
        }

        public static void StoreForReuse(VoxelGrid grid)
        {
            if (Grids.Count < 1000)
            {
                Grids.Push(grid);
            }
        }
    }
}
