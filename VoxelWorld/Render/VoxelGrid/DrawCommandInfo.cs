namespace VoxelWorld.Render.VoxelGrid
{
    internal readonly record struct DrawCommandInfo(int GridPositionOffset, int IndiceOffset, int IndiceCount, int VertexOffset, int VertexCount);
}
