using System.Numerics;

namespace VoxelWorld
{
    internal static class SphereGen
    {
        internal static float GetValue(Vector4 pos, float radius)
        {
            return radius - pos.Length();
        }
    }
}
