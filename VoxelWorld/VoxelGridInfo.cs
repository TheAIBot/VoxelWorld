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

        public void MakeDrawMethods(bool isBlocking)
        {
            Grid.Triangulize(this, isBlocking);
            //Grid.Pointizise(this, isBlocking);
            Grid = null;
        }

        public void DrawMesh(Frustum renderCheck)
        {
            if (meshVao == null || BoundingBox == null)
            {
                return;
            }
            if (!renderCheck.Intersects(BoundingBox))
            {
                return;
            }
            DrawCalls++;

            meshVao.Program.Use();
            meshVao.Draw();
        }

        public void DrawPoints(Frustum renderCheck)
        {
            if (pointsVao == null || BoundingBox == null)
            {
                return;
            }
            if (!renderCheck.Intersects(BoundingBox))
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
            if (meshVao != null || pointsVao != null)
            {
                MainThreadWork.QueueWorkAndWait(() =>
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
