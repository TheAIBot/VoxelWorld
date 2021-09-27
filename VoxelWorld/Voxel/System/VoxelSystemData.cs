namespace VoxelWorld
{
    internal class VoxelSystemData
    {
        public readonly int GridSize;
        public readonly float VoxelSize;
        public readonly PlanetGen WeightGen;
        private VoxelSystemData OneDown = null;

        private const int MaxDepth = 10;

        public VoxelSystemData(int gridSize, float voxelSize, PlanetGen generator)
        {
            this.GridSize = gridSize;
            this.VoxelSize = voxelSize;
            this.WeightGen = generator;
        }

        public VoxelSystemData GetOneDown()
        {
            if (OneDown == null)
            {
                OneDown = new VoxelSystemData(GridSize, VoxelSize / 2.0f, WeightGen);
            }

            return OneDown;
        }
    }
}
