using System.Numerics;

namespace VoxelWorld.Render.Box
{
    internal readonly struct BoxRenderInfo
    {
        public readonly Vector3 GridCenter;
        public readonly float GridSideLength;

        internal BoxRenderInfo(in Vector3 gridCenter, float gridSideLength)
        {
            GridCenter = gridCenter;
            GridSideLength = gridSideLength;
        }
    }
}
