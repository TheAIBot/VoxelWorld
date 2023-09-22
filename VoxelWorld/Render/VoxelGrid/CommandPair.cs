using VoxelWorld.Voxel;
using VoxelWorld.Voxel.Hierarchy;

namespace VoxelWorld.Render.VoxelGrid
{
    internal readonly record struct CommandPair(VoxelGridHierarchy Grid, GeometryData Geometry);
}
