using VoxelWorld.Voxel;
using VoxelWorld.Voxel.Hierarchy;

namespace VoxelWorld.Render.VoxelGrid
{
    internal readonly struct GridRenderCommand
    {
        public readonly VoxelGridHierarchy Grid;
        public readonly GeometryData GeoData;
        public readonly GridRenderCommandType CType;

        public GridRenderCommand(GridRenderCommandType cmd, VoxelGridHierarchy grid, GeometryData data)
        {
            Grid = grid;
            GeoData = data;
            CType = cmd;
        }
    }
}
