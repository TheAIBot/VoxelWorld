namespace VoxelWorld
{
    internal readonly struct DrawCommandInfo
    {
        public readonly int IndiceOffset;
        public readonly int IndiceCount;
        public readonly int VertexOffset;
        public readonly int VertexCount;

        public DrawCommandInfo(int indiceOffset, int indiceCount, int vertexOffset, int vertexCount)
        {
            IndiceOffset = indiceOffset;
            IndiceCount = indiceCount;
            VertexOffset = vertexOffset;
            VertexCount = vertexCount;
        }
    }
}
