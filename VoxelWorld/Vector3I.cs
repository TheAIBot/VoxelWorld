using System;
using System.Numerics;

namespace VoxelWorld
{
    internal readonly struct Vector3I
    {
        public readonly int X;
        public readonly int Y;
        public readonly int Z;

        public Vector3I(int x, int y, int z)
        {
            this.X = x;
            this.Y = y;
            this.Z = z;
        }

        public static Vector3I operator +(Vector3I a, Vector3I b)
        {
            return new Vector3I(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
        }

        public static Vector3I operator -(Vector3I a, Vector3I b)
        {
            return new Vector3I(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
        }

        public static Vector3I operator *(Vector3I a, Vector3I b)
        {
            return new Vector3I(a.X * b.X, a.Y * b.Y, a.Z * b.Z);
        }

        public static Vector3I operator /(Vector3I a, int b)
        {
            return new Vector3I(a.X / b, a.Y / b, a.Z / b);
        }

        public static bool operator ==(Vector3I a, Vector3I b)
        {
            return a.X == b.X && a.Y == b.Y && a.Z == b.Z;
        }

        public static bool operator !=(Vector3I a, Vector3I b)
        {
            return !(a == b);
        }

        public Vector3 AsFloatVector3()
        {
            return new Vector3(X, Y, Z);
        }

        public override bool Equals(object obj)
        {
            if (obj is Vector3I v)
            {
                return v == this;
            }
            return false;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(X, Y, Z);
        }
    }
}
