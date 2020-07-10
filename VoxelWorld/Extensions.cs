using System;
using System.Numerics;

namespace VoxelWorld
{
    internal static class Extensions
    {
        private static readonly Vector3I[] DirectionVectors = new Vector3I[]
        {
            new Vector3I(1, 0, 0),
            new Vector3I(-1, 0, 0),
            new Vector3I(0, 1, 0),
            new Vector3I(0, -1, 0),
            new Vector3I(0, 0, 1),
            new Vector3I(0, 0, -1),
        };

        public static Direction Opposite(this Direction dir)
        {
            switch (dir)
            {
                case Direction.Right:
                    return Direction.Left;
                case Direction.Left:
                    return Direction.Right;
                case Direction.Up:
                    return Direction.Down;
                case Direction.Down:
                    return Direction.Up;
                case Direction.Forward:
                    return Direction.Backward;
                case Direction.Backward:
                    return Direction.Forward;
                default:
                    throw new Exception($"Invalid direction: {(int)dir}");
            }
        }

        public static Vector3I AsVector3(this Direction dir)
        {
            return DirectionVectors[(int)dir];
        }
    }
}
