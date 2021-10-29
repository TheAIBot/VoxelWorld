using System;
using System.Diagnostics;
using System.Numerics;

namespace VoxelWorld.Voxel
{
    internal readonly struct GridPos : IEquatable<GridPos>
    {
        private readonly int X;
        private readonly int Y;
        private readonly int Z;
        private readonly int Level;

        public GridPos(int x, int y, int z, int level)
        {
            X = x;
            Y = y;
            Z = z;
            Level = level;
        }

        public static GridPos RootPos()
        {
            return new GridPos(0, 0, 0, 1);
        }

        public GridPos GoDownTree()
        {
            return new GridPos(X << 1, Y << 1, Z << 1, Level + 1);
        }

        public GridPos GoUpTree()
        {
            //Need to do logical shift otherwise could be wrong when
            //going up from level 32 due to sign extension
            return new GridPos((int)((uint)X >> 1), (int)((uint)Y >> 1), (int)((uint)Z >> 1), Level - 1);
        }

        public bool TryMove(in GridOffset offset, out GridPos gridPos)
        {
            return TryMove(offset.X, offset.Y, offset.Z, out gridPos);
        }

        public bool TryMove(int xOffset, int yOffset, int zOffset, out GridPos gridPos)
        {
            gridPos = Move(xOffset, yOffset, zOffset);
            int mask = (1 << Level) - 1;
            return gridPos.X == (gridPos.X & mask) &&
                   gridPos.Y == (gridPos.Y & mask) &&
                   gridPos.Z == (gridPos.Z & mask);
        }

        public GridPos Move(in GridOffset offset)
        {
            return Move(offset.X, offset.Y, offset.Z);
        }

        public GridPos Move(int xOffset, int yOffset, int zOffset)
        {
            return new GridPos(X + xOffset, Y + yOffset, Z + zOffset, Level);
        }

        public bool IsRootLevel()
        {
            return Level == 1;
        }

        public override bool Equals(object obj)
        {
            return obj is GridPos pos && Equals(pos);

        }

        public bool Equals(GridPos other)
        {
            return X == other.X &&
                   Y == other.Y &&
                   Z == other.Z &&
                   Level == other.Level;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(X, Y, Z, Level);
        }
    }
}
