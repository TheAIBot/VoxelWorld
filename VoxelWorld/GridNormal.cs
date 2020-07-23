using OpenGL;
using System;
using System.Numerics;

namespace VoxelWorld
{
    internal struct GridNormal
    {
        public bool Xp;
        public bool Xm;
        public bool Yp;
        public bool Ym;
        public bool Zp;
        public bool Zm;

        public bool CanSee(Vector3 lookDir)
        {
            if (lookDir.X > 0 && Xp)
            {
                return true;
            }
            else if (lookDir.X < 0 && Xm)
            {
                return true;
            }
            if (lookDir.Y > 0 && Yp)
            {
                return true;
            }
            else if (lookDir.Y < 0 && Ym)
            {
                return true;
            }
            if (lookDir.Z > 0 && Zp)
            {
                return true;
            }
            else if (lookDir.Z < 0 && Zm)
            {
                return true;
            }

            return false;
        }

        public void AddNormal(GridNormal normal)
        {
            Xp |= normal.Xp;
            Xm |= normal.Xm;
            Yp |= normal.Yp;
            Ym |= normal.Ym;
            Zp |= normal.Zp;
            Zm |= normal.Zm;
        }
    }
}
