namespace VoxelWorld
{
    internal readonly struct CommandPair
    {
        public readonly VoxelGridHierarchy Grid;
        public readonly GeometryData Geom;

        public CommandPair(VoxelGridHierarchy grid, GeometryData geometry)
        {
            this.Grid = grid;
            this.Geom = geometry;
        }
    }
}
