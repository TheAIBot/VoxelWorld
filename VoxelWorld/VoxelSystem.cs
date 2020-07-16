using OpenGL;
using System;
using System.Collections.Generic;
using System.Numerics;

namespace VoxelWorld
{

    internal class VoxelSystem
    {
        private readonly Dictionary<Vector3I, VoxelHierarchy> Grids = new Dictionary<Vector3I, VoxelHierarchy>();
        private readonly Vector3 Center;
        private float VoxelSize;
        private readonly int GridSize;
        private readonly Func<Vector3, float> WeightGen;

        public VoxelSystem(int gridSize, Vector3 center, float voxelSize, Func<Vector3, float> generator)
        {
            this.Center = center;
            this.VoxelSize = voxelSize;
            this.GridSize = gridSize;
            this.WeightGen = generator;
        }

        /*
        public async void TestFindRelevantGrids()
        {
            Vector3I searchCenter = new Vector3I(0, 0, 0);

            ExecutionDataflowBlockOptions options = new ExecutionDataflowBlockOptions();
            options.MaxDegreeOfParallelism = 7;

            TransformBlock<Vector3I, bool> toCheck = null;

            void AddPos(Vector3I pos)
            {
                toCheck.Post(pos);
            }


            toCheck = new TransformBlock<Vector3I, bool>(gridPos =>
            {
                Vector3 gridCenter = Center + gridPos.AsFloatVector3() * (GridSize - 2) * VoxelSize;
                VoxelGridInfo grid = new VoxelGridInfo();

                if (!TryAddGrid(gridPos, grid))
                {
                    grid.Dispose();
                    return false;
                }

                grid.GenerateGrid(GridSize, gridCenter, VoxelSize, WeightGen);
                if (grid.IsgridEmpty())
                {
                    if (TryRemoveGrid(gridPos))
                    {
                        grid.Dispose();
                    }
                    return false;
                }

                grid.Interpolate();
                //grid.SmoothGrid(1);
                grid.MakeDrawMethods(false);


                foreach (Direction dir in grid.GetDirectionsOfMissingNeighbors())
                {
                    AddPos(gridPos + dir.AsVector3());
                }

                return true;
            }, options);

            while (Grids.Count == 0)
            {
                toCheck.Post(searchCenter);
                searchCenter = new Vector3I(searchCenter.X + 1, 0, 0);

                if (toCheck.Receive())
                {
                    break;
                }
            }

            while (true)
            {
                //Shit way of checking if it's done generating
                if (toCheck.InputCount == 0)
                {
                    await Task.Delay(1000);
                    if (toCheck.InputCount == 0)
                    {
                        return;
                    }
                }
                await Task.Delay(1000);
            }
        }
        */

        public void TestResizeToFindFirstGrid()
        {
            while (true)
            {
                Vector3I gridPos = new Vector3I(0, 0, 0);
                Vector3 gridCenter = Center + gridPos.AsFloatVector3() * GridSize * VoxelSize;
                VoxelGridInfo grid = new VoxelGridInfo(gridCenter);

                grid.GenerateGridAction(GridSize, VoxelSize, WeightGen, Matrix4.Identity, new Vector3(0, 0, 0))();
                if (grid.IsEmpty)
                {
                    grid.Dispose();
                    VoxelSize *= 2;
                    continue;
                }


                if (grid.VoxelsAtEdge)
                {
                    grid.Dispose();
                    VoxelSize *= 2;
                    continue;
                }


                grid.Dispose();

                VoxelHierarchy hir = new VoxelHierarchy(GridSize, gridCenter, VoxelSize, WeightGen, 0);
                hir.Generate(Matrix4.Identity, new Vector3(0, 0, 0));

                if (!TryAddGrid(gridPos, hir))
                {
                    Console.Error.WriteLine("failed to add hirearchy to system.");
                }



                break;
            }
        }

        private bool TryAddGrid(Vector3I gridPos, VoxelHierarchy grid)
        {
            lock (Grids)
            {
                if (!Grids.TryAdd(gridPos, grid))
                {
                    return false;
                }

                //foreach (Direction dir in Enum.GetValues(typeof(Direction)))
                //{
                //    if (Grids.TryGetValue(gridPos + dir.AsVector3(), out VoxelHierarchy neighbor))
                //    {
                //        grid.AddNeighbor(neighbor, dir);
                //        neighbor.AddNeighbor(grid, dir.Opposite());
                //    }
                //}
            }

            return true;
        }

        private bool TryRemoveGrid(Vector3I gridPos)
        {
            lock (Grids)
            {
                return Grids.Remove(gridPos);
            }
        }

        public void CheckVoxelResolution(PlayerCamera camera, Frustum renderCheck)
        {
            lock (Grids)
            {
                foreach (var grid in Grids.Values)
                {
                    grid.CheckAndIncreaseResolution(camera, renderCheck);
                }
            }
        }
    }
}
