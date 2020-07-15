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

        public bool CanSee(Matrix4 model_rotation, Vector3 lookDir)
        {
            Vector3 rotLookDir = model_rotation * lookDir;
            //Console.WriteLine(rotLookDir);

            if (rotLookDir.X > 0 && Xp)
            {
                return true;
            }
            else if (rotLookDir.X < 0 && Xm)
            {
                return true;
            }
            if (rotLookDir.Y > 0 && Yp)
            {
                return true;
            }
            else if (rotLookDir.Y < 0 && Ym)
            {
                return true;
            }
            if (rotLookDir.Z > 0 && Zp)
            {
                return true;
            }
            else if (rotLookDir.Z < 0 && Zm)
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
