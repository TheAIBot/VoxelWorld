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
        internal VoxelSystemData FirstLevelSystemData;
        private readonly ModelTransformations ModelTrans = new ModelTransformations();

        public Matrix4 Model { get { return ModelTrans.Rotation; } }

        public VoxelSystem(int gridSize, Vector3 center, float voxelSize, PlanetGen generator)
        {
            this.Center = center;
            this.FirstLevelSystemData = new VoxelSystemData(gridSize, voxelSize, generator);
        }

        public void TestResizeToFindFirstGrid()
        {
            VoxelGrid vGrid = new VoxelGrid(new Vector3(0, 0, 0), FirstLevelSystemData);
            while (true)
            {
                Vector3I gridPos = new Vector3I(0, 0, 0);
                Vector3 gridCenter = Center + gridPos.AsFloatVector3() * FirstLevelSystemData.GridSize * FirstLevelSystemData.VoxelSize;
                VoxelGridHierarchy grid = new VoxelGridHierarchy(gridCenter, FirstLevelSystemData.GridSize, FirstLevelSystemData.VoxelSize);
                
                vGrid.Repurpose(gridCenter, FirstLevelSystemData);

                grid.GenerateGrid(FirstLevelSystemData, vGrid);
                if (grid.Grid.IsEmpty)
                {
                    grid.Dispose();
                    FirstLevelSystemData = new VoxelSystemData(FirstLevelSystemData.GridSize, FirstLevelSystemData.VoxelSize * 2.0f, FirstLevelSystemData.WeightGen);
                    continue;
                }


                if (grid.Grid.VoxelsAtEdge)
                {
                    grid.Dispose();
                    FirstLevelSystemData = new VoxelSystemData(FirstLevelSystemData.GridSize, FirstLevelSystemData.VoxelSize * 2.0f, FirstLevelSystemData.WeightGen);
                    continue;
                }
                FirstLevelSystemData = FirstLevelSystemData.GetOneDown();


                grid.Dispose();

                VoxelHierarchy hir = new VoxelHierarchy(gridCenter, FirstLevelSystemData);
                hir.Generate(new Vector3(0, 0, 0), FirstLevelSystemData, vGrid);

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

        public void UpdateModel(PlayerCamera camera, float yAngle)
        {
            ModelTrans.Update(camera, yAngle);
        }

        public void CheckVoxelResolution(Frustum renderCheck)
        {
            lock (Grids)
            {
                foreach (var grid in Grids.Values)
                {
                    grid.CheckAndIncreaseResolution(renderCheck, ModelTrans, FirstLevelSystemData);
                }
            }
        }
    }
}
