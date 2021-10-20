namespace VoxelWorld.Render.Box
{
    internal readonly struct BoxRenderCommand
    {
        public readonly BoxRenderCommandType CType;
        public readonly BoxRenderInfo BoxInfo;

        public BoxRenderCommand(BoxRenderCommandType commandType, BoxRenderInfo boxInfo)
        {
            CType = commandType;
            BoxInfo = boxInfo;
        }
    }
}
