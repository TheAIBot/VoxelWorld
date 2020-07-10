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
        private VoxelGrid Grid;
        private readonly VoxelGridInfo[] Neighbors = new VoxelGridInfo[Enum.GetValues(typeof(Direction)).Length];

        public VoxelGridInfo()
        {
        }

        public void GenerateGrid(int size, Vector3 center, float voxelSize, Func<Vector3, float> gen)
        {
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

        public void AddNeighbor(VoxelGridInfo grid, Direction dir)
        {
            Neighbors[(int)dir] = grid;
        }

        public void RemoveNeighbor(Direction dir)
        {
            Neighbors[(int)dir] = null;
        }

        public List<Direction> GetDirectionsOfMissingNeighbors()
        {
            List<Direction> dirs = new List<Direction>();
            foreach (Direction dir in Enum.GetValues(typeof(Direction)))
            {
                if (Neighbors[(int)dir] == null)
                {
                    dirs.Add(dir);
                }
            }

            return dirs;
        }

        public void MakeDrawMethods(bool isBlocking)
        {
            Grid.Triangulize(isBlocking);
            //Grid.Pointizise(isBlocking);
        }

        public void DrawMesh()
        {
            if (Grid?.MeshVao == null)
            {
                return;
            }
            Grid.MeshVao.Program.Use();
            Grid.MeshVao.Draw();
        }

        public void DrawPoints()
        {
            if (Grid?.PointVao == null)
            {
                return;
            }
            Grid.PointVao.Program.Use();
            Grid.PointVao.Draw();
        }

        public void Dispose()
        {
            if (Grid?.MeshVao != null || Grid?.PointVao != null)
            {
                MainThreadWork.QueueWorkAndWait(() =>
                {
                    Grid.MeshVao?.Dispose();
                    Grid.PointVao?.Dispose();

                    Grid.MeshVao = null;
                    Grid.PointVao = null;
                });
            }
        }
    }
}
