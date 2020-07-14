using OpenGL;
using System;
using System.Diagnostics;
using System.Numerics;

namespace VoxelWorld
{
    internal class VoxelGridInfo : IDisposable
    {
        private VoxelGrid Grid = null;
        public Vector3 Center { get; set; }
        public GridVAO meshVao = null;
        public GridVAO pointsVao = null;
        public AxisAlignedBoundingBox BoundingBox = null;
        public readonly object DisposeLock = new object();
        public bool HasBeenDisposed = false;

        public static int DrawCalls = 0;

        public void GenerateGrid(int size, Vector3 center, float voxelSize, Func<Vector3, float> gen)
        {
            Center = center;
            Grid =  VoxelGridStorage.GetGrid(size, center, voxelSize, gen);
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
        }

        public void PreCalculateGeometryData()
        {
            Grid.PreCalculateGeometryData();
        }

        public AxisAlignedBoundingBox GetBoundingBox()
        {
            BoundingBox = Grid.GetBoundingBox();
            return BoundingBox;
        }

        public GridNormal GetGridNormal()
        {
            return Grid.GetGridNormal();
        }

        public bool DrawMesh()
        {
            if (meshVao == null || BoundingBox == null)
            {
                return false;
            }
            DrawCalls++;

            meshVao.Program.Use();
            meshVao.Draw();

            return true;
        }

        public bool DrawPoints()
        {
            if (pointsVao == null || BoundingBox == null)
            {
                return false;
            }
            DrawCalls++;

            pointsVao.Program.Use();
            pointsVao.Program["mat_diff"].SetValue(new Vector4(Vector3.Abs(Center.Normalize()), 0.4f));
            pointsVao.Program["mat_spec"].SetValue(new Vector4(Vector3.Abs(Center.Normalize()), 0.4f));
            pointsVao.Draw();

            return true;
        }

        public void Dispose()
        {
            lock (DisposeLock)
            {
                HasBeenDisposed = true;
            }

            Debug.Assert(Grid != null);
            VoxelGridStorage.StoreForReuse(Grid);
            Grid = null;

            if (meshVao != null || pointsVao != null)
            {
                MainThreadWork.QueueWork(x =>
                {
                    if (meshVao != null)
                    {
                        x.StoreGridVAOForReuse(meshVao);
                    }
                    if (pointsVao != null)
                    {
                        x.StoreGridVAOForReuse(pointsVao);
                    }

                    meshVao = null;
                    pointsVao = null;
                });
            }
        }
    }
}
