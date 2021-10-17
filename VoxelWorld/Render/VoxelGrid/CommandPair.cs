using VoxelWorld.Voxel;
using VoxelWorld.Voxel.Hierarchy;

namespace VoxelWorld.Render.VoxelGrid
{
    internal readonly struct CommandPair
    {
        public readonly VoxelGridHierarchy Grid;
        public readonly GeometryData Geom;

        public CommandPair(VoxelGridHierarchy grid, GeometryData geometry)
        {
            Grid = grid;
            Geom = geometry;
        }
    }
}
