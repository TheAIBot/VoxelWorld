using OpenGL;
using System;
using System.Collections.Generic;
using System.Numerics;

namespace VoxelWorld
{
    internal class ModelTransformations
    {
        public Matrix4 Rotation = Matrix4.Identity;
        public Matrix4 RevRotation = Matrix4.Identity;
        public Vector3 Translation = new Vector3(0, 0, 0);
        public Vector3 RotatedLookDir = new Vector3(0, 0, 0);
        public Vector3 CameraPos = new Vector3(0, 0, 0);

        public void Update(PlayerCamera camera, float yAngle)
        {
            Rotation = Matrix4.CreateRotationY(yAngle);
            RevRotation = Matrix4.CreateRotationY(-yAngle);
            RotatedLookDir = Rotation * camera.LookDirection;
            CameraPos = camera.CameraPos;
        }
    }

    internal class VoxelSystemData
    {
        public readonly int GridSize;
        public readonly float VoxelSize;
        public readonly PlanetGen WeightGen;
        private VoxelSystemData OneDown = null;

        private const int MaxDepth = 10;

        public VoxelSystemData(int gridSize, float voxelSize, PlanetGen generator)
        {
            this.GridSize = gridSize;
            this.VoxelSize = voxelSize;
            this.WeightGen = generator;
        }

        public VoxelSystemData GetOneDown()
        {
            if (OneDown == null)
            {
                OneDown = new VoxelSystemData(GridSize, VoxelSize / 2.0f, WeightGen);
            }

            return OneDown;
        }
    }


    internal class VoxelSystem
    {
        private readonly Dictionary<Vector3I, VoxelHierarchy> Grids = new Dictionary<Vector3I, VoxelHierarchy>();
        private readonly Vector3 Center;
        private VoxelSystemData FirstLevelSystemData;
        private readonly ModelTransformations ModelTrans = new ModelTransformations();

        public Matrix4 Model { get { return ModelTrans.Rotation; } }

        public VoxelSystem(int gridSize, Vector3 center, float voxelSize, PlanetGen generator)
        {
            this.Center = center;
            this.FirstLevelSystemData = new VoxelSystemData(gridSize, voxelSize, generator);
        }

        public void TestResizeToFindFirstGrid()
        {
            while (true)
            {
                Vector3I gridPos = new Vector3I(0, 0, 0);
                Vector3 gridCenter = Center + gridPos.AsFloatVector3() * FirstLevelSystemData.GridSize * FirstLevelSystemData.VoxelSize;
                VoxelGridHierarchy grid = new VoxelGridHierarchy(gridCenter, FirstLevelSystemData.GridSize, FirstLevelSystemData.VoxelSize);

                grid.GenerateGrid(FirstLevelSystemData);
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
                hir.Generate(new Vector3(0, 0, 0), FirstLevelSystemData);

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
