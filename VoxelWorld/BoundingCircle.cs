using System;
using System.Numerics;

namespace VoxelWorld
{
    public readonly record struct BoundingCircle(Vector3 Center, float Radius)
    {
        public BoundingCircle AddBoundingCircle(BoundingCircle circle)
        {
            float radius = Math.Max(Radius, (Center - circle.Center).Length() + circle.Radius);
            return new BoundingCircle(Center, radius);
        }
    }
}
