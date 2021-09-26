namespace VoxelWorld
{
    internal readonly struct GridRenderCommand
    {
        public readonly VoxelGridHierarchy Grid;
        public readonly GeometryData GeoData;
        public readonly GridRenderCommandType CType;

        public GridRenderCommand(GridRenderCommandType cmd, VoxelGridHierarchy grid, GeometryData data)
        {
            this.Grid = grid;
            this.GeoData = data;
            this.CType = cmd;
        }
    }
}
