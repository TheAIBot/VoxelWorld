using VoxelWorld.Voxel;
using VoxelWorld.Voxel.Hierarchy;

namespace VoxelWorld.Render.VoxelGrid
{
    internal readonly record struct GridRenderCommand(GridRenderCommandType CType, VoxelGridHierarchy Grid, GeometryData GeoData);
}
