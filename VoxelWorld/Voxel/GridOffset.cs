namespace VoxelWorld
{
    public readonly struct GridOffset
    {
        public readonly int X;
        public readonly int Y;
        public readonly int Z;

        public GridOffset(int x, int y, int z)
        {
            X = x;
            Y = y;
            Z = z;
        }
    }
}
