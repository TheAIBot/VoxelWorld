using OpenGL;
using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;

namespace VoxelWorld
{
    internal class VoxelGridInfo : IDisposable
    {
        private VoxelGrid Grid = null;
        public Vector3 Center { get; set; }
        public VAO meshVao = null;
        public VAO pointsVao = null;
        public AxisAlignedBoundingBox BoundingBox = null;
        public readonly object DisposeLock = new object();
        public bool HasBeenDisposed = false;

        public static int DrawCalls = 0;

        public void GenerateGrid(int size, Vector3 center, float voxelSize, Func<Vector3, float> gen)
        {
            Center = center;
            Grid = new VoxelGrid(size, center, voxelSize, gen);
            Grid.Randomize();

        }

        public bool IsgridEmpty()
        {
            return Grid.IsEmpty();
        }

        public bool EdgePointsUsed()
        {
            return Grid.EdgePointsUsed();
        }

        public void Interpolate()
        {
            Grid.Interpolate();
        }

        public void SmoothGrid(int iterations)
        {
            Grid.Smooth(iterations);
        }

        public void MakeDrawMethods()
        {
            Grid.Triangulize(this);
            //Grid.Pointizise(this, isBlocking);
            Grid = null;
        }

        public AxisAlignedBoundingBox GetBoundingBox()
        {
            BoundingBox = Grid.GetBoundingBox();
            return BoundingBox;
        }

        public void DrawMesh()
        {
            if (meshVao == null || BoundingBox == null)
            {
                return;
            }
            DrawCalls++;

            meshVao.Program.Use();
            meshVao.Draw();
        }

        public void DrawPoints()
        {
            if (pointsVao == null || BoundingBox == null)
            {
                return;
            }
            DrawCalls++;

            pointsVao.Program.Use();
            pointsVao.Program["mat_diff"].SetValue(new Vector4(Vector3.Abs(Center.Normalize()), 0.4f));
            pointsVao.Program["mat_spec"].SetValue(new Vector4(Vector3.Abs(Center.Normalize()), 0.4f));
            pointsVao.Draw();
        }

        public void Dispose()
        {
            lock (DisposeLock)
            {
                HasBeenDisposed = true;
            }

            if (meshVao != null || pointsVao != null)
            {
                MainThreadWork.QueueWork(() =>
                {
                    meshVao?.Dispose();
                    pointsVao?.Dispose();

                    meshVao = null;
                    pointsVao = null;
                });
            }
        }
    }
}
